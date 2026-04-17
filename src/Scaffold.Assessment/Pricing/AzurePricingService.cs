using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.Pricing;

public class AzurePricingService : IAzurePricingService
{
    private const string BaseUrl = "https://prices.azure.com/api/retail/prices?api-version=2023-01-01-preview";
    private const int HoursPerMonth = 730;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AzurePricingService> _logger;

    public AzurePricingService(HttpClient httpClient, IMemoryCache cache, ILogger<AzurePricingService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<RegionPricing>> GetPricingForTierAsync(string serviceTier, string computeSize, int storageGb, DatabasePlatform platform = DatabasePlatform.SqlServer)
    {
        var cacheKey = $"pricing:{serviceTier}:{computeSize}:{storageGb}";
        if (_cache.TryGetValue(cacheKey, out List<RegionPricing>? cached) && cached is not null)
            return cached;

        try
        {
            var filter = BuildFilter(serviceTier, computeSize);
            var priceItems = await FetchAllPagesAsync(filter);

            if (priceItems.Count == 0)
            {
                _logger.LogWarning("No pricing data returned for tier={Tier}, compute={Compute}", serviceTier, computeSize);
                return [];
            }

            // Filter out Spot, Low Priority, and DevTest items
            priceItems = priceItems
                .Where(p => !string.IsNullOrEmpty(p.ArmRegionName))
                .Where(p => !p.SkuName.Contains("Spot", StringComparison.OrdinalIgnoreCase))
                .Where(p => !p.SkuName.Contains("Low Priority", StringComparison.OrdinalIgnoreCase))
                .Where(p => !p.MeterName.Contains("Spot", StringComparison.OrdinalIgnoreCase))
                .Where(p => !p.MeterName.Contains("Low Priority", StringComparison.OrdinalIgnoreCase))
                .Where(p => !p.ProductName.Contains("DevTest", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var results = priceItems
                .GroupBy(p => p.ArmRegionName)
                .Select(g => CalculateRegionPricing(g.Key, g.ToList(), storageGb, serviceTier))
                .Where(r => r.EstimatedMonthlyCostUsd > 0)
                .OrderBy(r => r.EstimatedMonthlyCostUsd)
                .ToList();

            _cache.Set(cacheKey, results, CacheDuration);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch pricing for tier={Tier}, compute={Compute}", serviceTier, computeSize);
            return [];
        }
    }

    public async Task<List<string>> GetAvailableRegionsAsync(DatabasePlatform platform = DatabasePlatform.SqlServer)
    {
        var cacheKey = $"pricing:regions:{platform}";
        if (_cache.TryGetValue(cacheKey, out List<string>? cached) && cached is not null)
            return cached;

        try
        {
            // Use a narrow query to get regions quickly — different service per platform
            var filter = platform switch
            {
                DatabasePlatform.PostgreSql =>
                    "serviceName eq 'Azure Database for PostgreSQL' and priceType eq 'Consumption' and currencyCode eq 'USD' and armSkuName eq 'GP_Standard_D2s_v3'",
                _ =>
                    "serviceName eq 'SQL Database' and priceType eq 'Consumption' and currencyCode eq 'USD' and armSkuName eq 'SQLDB_GP_Compute_Gen5_2'"
            };
            var priceItems = await FetchAllPagesAsync(filter);

            var regions = priceItems
                .Where(p => !string.IsNullOrEmpty(p.ArmRegionName))
                .Select(p => p.ArmRegionName)
                .Distinct()
                .Order()
                .ToList();

            _cache.Set(cacheKey, regions, CacheDuration);
            return regions;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch available regions");
            return [];
        }
    }

    private static string BuildFilter(string serviceTier, string computeSize)
    {
        return serviceTier switch
        {
            "SQL Server on Azure VM" => BuildVmFilter(computeSize),
            "Azure SQL Managed Instance" => BuildManagedInstanceFilter(computeSize),
            "PostgreSQL on Azure VM" => BuildPostgreSqlVmFilter(computeSize),
            "Azure Database for PostgreSQL - Flexible Server" => BuildFlexibleServerFilter(computeSize),
            _ => BuildSqlDatabaseFilter(computeSize) // SQL Database + Hyperscale
        };
    }

    private static string BuildFlexibleServerFilter(string computeSize)
    {
        // serviceName eq 'Azure Database for PostgreSQL' for Flexible Server
        // computeSize maps directly as armSkuName (e.g., GP_Standard_D2s_v3)
        return $"serviceName eq 'Azure Database for PostgreSQL' and priceType eq 'Consumption' and currencyCode eq 'USD'" +
               $" and armSkuName eq '{computeSize}'";
    }

    private static string BuildPostgreSqlVmFilter(string computeSize)
    {
        // Same as SQL VM filter but Linux instead of Windows
        return $"serviceName eq 'Virtual Machines' and priceType eq 'Consumption' and currencyCode eq 'USD'" +
               $" and armSkuName eq '{computeSize}'" +
               $" and contains(productName, 'Linux')";
    }

    private static string BuildVmFilter(string computeSize)
    {
        // Filter by exact VM SKU (e.g. Standard_D2s_v5) and Windows only (for SQL Server)
        return $"serviceName eq 'Virtual Machines' and priceType eq 'Consumption' and currencyCode eq 'USD'" +
               $" and armSkuName eq '{computeSize}'" +
               $" and contains(productName, 'Windows')";
    }

    private static string BuildSqlDatabaseFilter(string computeSize)
    {
        // Map computeSize (e.g. GP_Gen5_2) to armSkuName (SQLDB_GP_Compute_Gen5_2)
        // DTU tiers (Basic, S0-S3, P1-P4) use different naming
        var armSkuName = MapSqlDbArmSkuName(computeSize);

        var filter = $"serviceName eq 'SQL Database' and priceType eq 'Consumption' and currencyCode eq 'USD'";

        if (!string.IsNullOrEmpty(armSkuName))
            filter += $" and armSkuName eq '{armSkuName}'";
        else
            filter += $" and contains(meterName, '{computeSize}')";

        return filter;
    }

    private static string BuildManagedInstanceFilter(string computeSize)
    {
        // Map GP_Gen5_4 → SQLMI_GP_Compute_Gen5_4 (some vCore counts have empty armSkuName)
        var armSkuName = MapMiArmSkuName(computeSize);

        var filter = $"serviceName eq 'SQL Managed Instance' and priceType eq 'Consumption' and currencyCode eq 'USD'";

        if (!string.IsNullOrEmpty(armSkuName))
            filter += $" and armSkuName eq '{armSkuName}'";
        else
        {
            // Fallback: filter by product name keywords
            var tierKeyword = computeSize.StartsWith("GP_") ? "General Purpose" : "Business Critical";
            filter += $" and contains(productName, '{tierKeyword}') and contains(productName, 'Gen5')";
        }

        return filter;
    }

    /// <summary>Maps our compute size to the Azure armSkuName for SQL Database.</summary>
    private static string? MapSqlDbArmSkuName(string computeSize)
    {
        // vCore tiers: GP_Gen5_2 → SQLDB_GP_Compute_Gen5_2
        if (computeSize.StartsWith("GP_Gen5_") || computeSize.StartsWith("BC_Gen5_") || computeSize.StartsWith("HS_Gen5_"))
        {
            var parts = computeSize.Split('_'); // e.g. ["GP", "Gen5", "2"]
            if (parts.Length == 3)
                return $"SQLDB_{parts[0]}_Compute_{parts[1]}_{parts[2]}";
        }

        // DTU tiers map differently — return null to use meterName fallback
        return null;
    }

    /// <summary>Maps our compute size to the Azure armSkuName for Managed Instance.</summary>
    private static string? MapMiArmSkuName(string computeSize)
    {
        // GP_Gen5_8 → SQLMI_GP_Compute_Gen5_8 (but smaller vCore counts may have empty armSkuName)
        if (computeSize.StartsWith("GP_Gen5_") || computeSize.StartsWith("BC_Gen5_"))
        {
            var parts = computeSize.Split('_');
            if (parts.Length == 3)
                return $"SQLMI_{parts[0]}_Compute_{parts[1]}_{parts[2]}";
        }
        return null;
    }

    private static RegionPricing CalculateRegionPricing(string armRegionName, List<AzurePriceItem> items, int storageGb, string serviceTier)
    {
        decimal computeCost;

        if (serviceTier == "SQL Server on Azure VM" || serviceTier == "PostgreSQL on Azure VM")
        {
            // For VMs: pick the single hourly rate (filter already ensures correct OS: Windows for SQL, Linux for PG)
            var vmItem = items
                .Where(i => i.UnitOfMeasure.Contains("Hour", StringComparison.OrdinalIgnoreCase))
                .Where(i => i.MeterName == items.First().MeterName.Replace(" Spot", "").Replace(" Low Priority", ""))
                .OrderByDescending(i => i.RetailPrice) // Windows is more expensive than Linux
                .FirstOrDefault();
            computeCost = (vmItem?.RetailPrice ?? 0) * HoursPerMonth;
        }
        else
        {
            // For SQL DB/MI: pick the single compute (vCore) meter — the price already reflects the total for that SKU
            var computeItem = items
                .Where(i => i.UnitOfMeasure.Contains("Hour", StringComparison.OrdinalIgnoreCase))
                .Where(i => !i.MeterName.Contains("Zone Redundancy", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
            computeCost = (computeItem?.RetailPrice ?? 0) * HoursPerMonth;
        }

        // Storage cost: pick a single per-GB/month storage meter
        var storageItem = items
            .Where(i => i.UnitOfMeasure.Contains("GB", StringComparison.OrdinalIgnoreCase))
            .Where(i => i.MeterName.Contains("Storage", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
        var storageCost = (storageItem?.RetailPrice ?? 0) * storageGb;

        var displayName = items.FirstOrDefault()?.Location ?? armRegionName;

        return new RegionPricing
        {
            ArmRegionName = armRegionName,
            DisplayName = displayName,
            EstimatedMonthlyCostUsd = computeCost + storageCost
        };
    }

    private async Task<List<AzurePriceItem>> FetchAllPagesAsync(string filter)
    {
        var allItems = new List<AzurePriceItem>();
        var url = $"{BaseUrl}&$filter={Uri.EscapeDataString(filter)}";

        while (!string.IsNullOrEmpty(url))
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Azure Pricing API returned {StatusCode} for {Url}", response.StatusCode, url);
                break;
            }

            var page = await response.Content.ReadFromJsonAsync<PricingApiResponse>(JsonOptions);
            if (page?.Items is not null)
                allItems.AddRange(page.Items);

            url = page?.NextPageLink;
        }

        return allItems;
    }

    private sealed class PricingApiResponse
    {
        public List<AzurePriceItem> Items { get; set; } = [];
        public string? NextPageLink { get; set; }
    }
}
