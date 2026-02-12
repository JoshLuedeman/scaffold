using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Scaffold.Assessment.Pricing;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests;

public class AzurePricingServiceTests : IDisposable
{
    private readonly MockHttpMessageHandler _handler;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AzurePricingService> _logger;

    public AzurePricingServiceTests()
    {
        _handler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_handler);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _logger = new LoggerFactory().CreateLogger<AzurePricingService>();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _cache.Dispose();
    }

    private AzurePricingService CreateService() => new(_httpClient, _cache, _logger);

    private static AzurePriceItem MakeItem(
        string armRegionName = "eastus",
        string location = "East US",
        decimal retailPrice = 0.10m,
        string unitOfMeasure = "1 Hour",
        string meterName = "vCore",
        string skuName = "Standard",
        string productName = "SQL Database",
        string serviceName = "SQL Database",
        string type = "Consumption") => new()
    {
        ArmRegionName = armRegionName,
        Location = location,
        RetailPrice = retailPrice,
        UnitPrice = retailPrice,
        UnitOfMeasure = unitOfMeasure,
        MeterName = meterName,
        SkuName = skuName,
        ProductName = productName,
        ServiceName = serviceName,
        CurrencyCode = "USD",
        Type = type,
        IsPrimaryMeterRegion = true
    };

    private void EnqueueItems(params AzurePriceItem[] items) =>
        _handler.EnqueueResponse(new { Items = items.ToList(), NextPageLink = (string?)null });

    [Fact]
    public async Task GetPricingForTierAsync_SqlDatabase_BuildsCorrectFilter()
    {
        EnqueueItems(MakeItem());
        var svc = CreateService();

        await svc.GetPricingForTierAsync("Azure SQL Database", "GP_Gen5_2", 32);

        Assert.Single(_handler.RequestUrls);
        var url = Uri.UnescapeDataString(_handler.RequestUrls[0]);
        Assert.Contains("armSkuName eq 'SQLDB_GP_Compute_Gen5_2'", url);
        Assert.Contains("serviceName eq 'SQL Database'", url);
    }

    [Fact]
    public async Task GetPricingForTierAsync_VM_BuildsCorrectFilter()
    {
        EnqueueItems(MakeItem(productName: "Virtual Machines Dsv5 Series Windows", serviceName: "Virtual Machines"));
        var svc = CreateService();

        await svc.GetPricingForTierAsync("SQL Server on Azure VM", "Standard_D2s_v5", 0);

        var url = Uri.UnescapeDataString(_handler.RequestUrls[0]);
        Assert.Contains("armSkuName eq 'Standard_D2s_v5'", url);
        Assert.Contains("contains(productName, 'Windows')", url);
    }

    [Fact]
    public async Task GetPricingForTierAsync_ManagedInstance_BuildsCorrectFilter()
    {
        EnqueueItems(MakeItem(serviceName: "SQL Managed Instance"));
        var svc = CreateService();

        await svc.GetPricingForTierAsync("Azure SQL Managed Instance", "GP_Gen5_8", 32);

        var url = Uri.UnescapeDataString(_handler.RequestUrls[0]);
        Assert.Contains("armSkuName eq 'SQLMI_GP_Compute_Gen5_8'", url);
    }

    [Fact]
    public async Task GetPricingForTierAsync_CalculatesComputeCost_SqlDatabase()
    {
        EnqueueItems(MakeItem(retailPrice: 0.304434m, unitOfMeasure: "1 Hour", meterName: "vCore"));
        var svc = CreateService();

        var results = await svc.GetPricingForTierAsync("Azure SQL Database", "GP_Gen5_2", 0);

        Assert.Single(results);
        Assert.Equal(0.304434m * 730, results[0].EstimatedMonthlyCostUsd, 2);
    }

    [Fact]
    public async Task GetPricingForTierAsync_CalculatesComputeCost_VM()
    {
        EnqueueItems(MakeItem(
            retailPrice: 0.188m,
            unitOfMeasure: "1 Hour",
            meterName: "D2s v5",
            productName: "Virtual Machines Dsv5 Series Windows",
            serviceName: "Virtual Machines"));
        var svc = CreateService();

        var results = await svc.GetPricingForTierAsync("SQL Server on Azure VM", "Standard_D2s_v5", 0);

        Assert.Single(results);
        Assert.Equal(0.188m * 730, results[0].EstimatedMonthlyCostUsd, 2);
    }

    [Fact]
    public async Task GetPricingForTierAsync_FiltersOutSpotAndLowPriority()
    {
        EnqueueItems(
            MakeItem(retailPrice: 0.188m, meterName: "D2s v5", skuName: "Standard",
                productName: "Virtual Machines Dsv5 Series Windows", serviceName: "Virtual Machines"),
            MakeItem(retailPrice: 0.05m, meterName: "D2s v5", skuName: "D2s v5 Spot",
                productName: "Virtual Machines Dsv5 Series Windows", serviceName: "Virtual Machines"),
            MakeItem(retailPrice: 0.03m, meterName: "D2s v5 Low Priority", skuName: "Standard",
                productName: "Virtual Machines Dsv5 Series Windows", serviceName: "Virtual Machines"));
        var svc = CreateService();

        var results = await svc.GetPricingForTierAsync("SQL Server on Azure VM", "Standard_D2s_v5", 0);

        Assert.Single(results);
        Assert.Equal(0.188m * 730, results[0].EstimatedMonthlyCostUsd, 2);
    }

    [Fact]
    public async Task GetPricingForTierAsync_FiltersOutDevTest()
    {
        EnqueueItems(MakeItem(retailPrice: 0.10m, productName: "SQL Database DevTest"));
        var svc = CreateService();

        var results = await svc.GetPricingForTierAsync("Azure SQL Database", "GP_Gen5_2", 0);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetPricingForTierAsync_ExcludesZoneRedundancy()
    {
        EnqueueItems(
            MakeItem(retailPrice: 0.304m, meterName: "vCore"),
            MakeItem(retailPrice: 0.091m, meterName: "Zone Redundancy vCore"));
        var svc = CreateService();

        var results = await svc.GetPricingForTierAsync("Azure SQL Database", "GP_Gen5_2", 0);

        Assert.Single(results);
        Assert.Equal(0.304m * 730, results[0].EstimatedMonthlyCostUsd, 2);
    }

    [Fact]
    public async Task GetPricingForTierAsync_IncludesStorageCost()
    {
        EnqueueItems(
            MakeItem(retailPrice: 0.304m, meterName: "vCore", unitOfMeasure: "1 Hour"),
            MakeItem(retailPrice: 0.115m, meterName: "Data Storage", unitOfMeasure: "1 GB/Month"));
        var svc = CreateService();

        var results = await svc.GetPricingForTierAsync("Azure SQL Database", "GP_Gen5_2", 100);

        Assert.Single(results);
        var expected = (0.304m * 730) + (0.115m * 100);
        Assert.Equal(expected, results[0].EstimatedMonthlyCostUsd, 2);
    }

    [Fact]
    public async Task GetPricingForTierAsync_ReturnsCachedResult()
    {
        EnqueueItems(MakeItem(retailPrice: 0.304m));
        var svc = CreateService();

        var first = await svc.GetPricingForTierAsync("Azure SQL Database", "GP_Gen5_2", 32);
        var second = await svc.GetPricingForTierAsync("Azure SQL Database", "GP_Gen5_2", 32);

        Assert.Single(_handler.RequestUrls);
        Assert.Equal(first.Count, second.Count);
    }

    [Fact]
    public async Task GetPricingForTierAsync_PaginatesViaNextPageLink()
    {
        _handler.EnqueueResponse(new
        {
            Items = new List<AzurePriceItem> { MakeItem(armRegionName: "eastus", retailPrice: 0.10m) },
            NextPageLink = "https://prices.azure.com/page2"
        });
        _handler.EnqueueResponse(new
        {
            Items = new List<AzurePriceItem> { MakeItem(armRegionName: "westus", retailPrice: 0.20m) },
            NextPageLink = (string?)null
        });
        var svc = CreateService();

        var results = await svc.GetPricingForTierAsync("Azure SQL Database", "GP_Gen5_2", 0);

        Assert.Equal(2, _handler.RequestUrls.Count);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetPricingForTierAsync_ReturnsEmptyOnApiError()
    {
        _handler.EnqueueResponse(new { }, HttpStatusCode.InternalServerError);
        var svc = CreateService();

        var results = await svc.GetPricingForTierAsync("Azure SQL Database", "GP_Gen5_2", 32);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetPricingForTierAsync_OrdersByRegionCost()
    {
        EnqueueItems(
            MakeItem(armRegionName: "westus", location: "West US", retailPrice: 0.50m),
            MakeItem(armRegionName: "eastus", location: "East US", retailPrice: 0.10m),
            MakeItem(armRegionName: "centralus", location: "Central US", retailPrice: 0.30m));
        var svc = CreateService();

        var results = await svc.GetPricingForTierAsync("Azure SQL Database", "GP_Gen5_2", 0);

        Assert.Equal(3, results.Count);
        Assert.Equal("eastus", results[0].ArmRegionName);
        Assert.Equal("centralus", results[1].ArmRegionName);
        Assert.Equal("westus", results[2].ArmRegionName);
    }

    [Fact]
    public async Task GetAvailableRegionsAsync_ReturnsDistinctSortedRegions()
    {
        EnqueueItems(
            MakeItem(armRegionName: "eastus"),
            MakeItem(armRegionName: "westus"),
            MakeItem(armRegionName: "eastus"));
        var svc = CreateService();

        var regions = await svc.GetAvailableRegionsAsync();

        Assert.Equal(2, regions.Count);
        Assert.Equal("eastus", regions[0]);
        Assert.Equal("westus", regions[1]);
    }

    [Theory]
    [InlineData("Basic")]
    [InlineData("S0")]
    [InlineData("P1")]
    public async Task MapSqlDbArmSkuName_DtuTiers_ReturnsNull(string computeSize)
    {
        EnqueueItems(MakeItem());
        var svc = CreateService();

        await svc.GetPricingForTierAsync("Azure SQL Database", computeSize, 32);

        var url = Uri.UnescapeDataString(_handler.RequestUrls[0]);
        Assert.Contains($"contains(meterName, '{computeSize}')", url);
        Assert.DoesNotContain("armSkuName eq", url);
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public List<string> RequestUrls { get; } = new();

        public void EnqueueResponse(object body, HttpStatusCode status = HttpStatusCode.OK)
        {
            var json = JsonSerializer.Serialize(body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            _responses.Enqueue(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUrls.Add(request.RequestUri!.ToString());
            return Task.FromResult(_responses.Count > 0
                ? _responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
