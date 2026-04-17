using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Scaffold.Api.Tests.Infrastructure;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;

namespace Scaffold.Api.Tests;

public class StubAzurePricingService : IAzurePricingService
{
    public Task<List<string>> GetAvailableRegionsAsync(DatabasePlatform platform = DatabasePlatform.SqlServer)
    {
        return Task.FromResult(new List<string> { "eastus", "westus2", "westeurope" });
    }

    public Task<List<RegionPricing>> GetPricingForTierAsync(string serviceTier, string computeSize, int storageGb, DatabasePlatform platform = DatabasePlatform.SqlServer)
    {
        return Task.FromResult(new List<RegionPricing>
        {
            new() { ArmRegionName = "westeurope", DisplayName = "West Europe", EstimatedMonthlyCostUsd = 150m },
            new() { ArmRegionName = "westus2", DisplayName = "West US 2", EstimatedMonthlyCostUsd = 120m },
            new() { ArmRegionName = "eastus", DisplayName = "East US", EstimatedMonthlyCostUsd = 100m },
        });
    }
}

public class PricingWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IAzurePricingService));
            if (descriptor != null) services.Remove(descriptor);

            services.AddSingleton<IAzurePricingService, StubAzurePricingService>();
        });
    }
}

public class PricingControllerTests : IClassFixture<PricingWebApplicationFactory>
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public PricingControllerTests(PricingWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetRegions_ReturnsRegionList()
    {
        var response = await _client.GetAsync("/api/pricing/regions");

        response.EnsureSuccessStatusCode();
        var regions = await response.Content.ReadFromJsonAsync<List<string>>(_jsonOptions);
        Assert.NotNull(regions);
        Assert.Equal(3, regions.Count);
    }

    [Fact]
    public async Task GetEstimate_ReturnsSortedPricing()
    {
        var response = await _client.GetAsync(
            "/api/pricing/estimate?service=Azure+SQL+Database&compute=GP_Gen5_2&storageGb=32");

        response.EnsureSuccessStatusCode();
        var pricing = await response.Content.ReadFromJsonAsync<List<RegionPricing>>(_jsonOptions);
        Assert.NotNull(pricing);
        Assert.True(pricing.Count > 1);
        for (int i = 1; i < pricing.Count; i++)
        {
            Assert.True(pricing[i].EstimatedMonthlyCostUsd >= pricing[i - 1].EstimatedMonthlyCostUsd);
        }
    }

    [Fact]
    public async Task GetEstimate_DefaultStorageGb()
    {
        var response = await _client.GetAsync(
            "/api/pricing/estimate?service=Azure+SQL+Database&compute=GP_Gen5_2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pricing = await response.Content.ReadFromJsonAsync<List<RegionPricing>>(_jsonOptions);
        Assert.NotNull(pricing);
        Assert.NotEmpty(pricing);
    }

    [Fact]
    public async Task GetEstimate_ReturnsCorrectNumberOfRegions()
    {
        var response = await _client.GetAsync(
            "/api/pricing/estimate?service=Azure+SQL+Database&compute=GP_Gen5_2&storageGb=32");

        response.EnsureSuccessStatusCode();
        var pricing = await response.Content.ReadFromJsonAsync<List<RegionPricing>>(_jsonOptions);
        Assert.NotNull(pricing);
        Assert.Equal(3, pricing.Count);
    }

    // ── Multi-platform support ──────────────────────────────────────

    [Theory]
    [InlineData(DatabasePlatform.SqlServer)]
    [InlineData(DatabasePlatform.PostgreSql)]
    public async Task GetRegions_WithPlatform_ReturnsRegions(DatabasePlatform platform)
    {
        var response = await _client.GetAsync($"/api/pricing/regions?platform={platform}");

        response.EnsureSuccessStatusCode();
        var regions = await response.Content.ReadFromJsonAsync<List<string>>(_jsonOptions);
        Assert.NotNull(regions);
        Assert.Equal(3, regions.Count);
    }

    [Fact]
    public async Task GetRegions_NoPlatform_DefaultsToSqlServer()
    {
        // Omitting platform parameter should default to SqlServer and still work
        var response = await _client.GetAsync("/api/pricing/regions");

        response.EnsureSuccessStatusCode();
        var regions = await response.Content.ReadFromJsonAsync<List<string>>(_jsonOptions);
        Assert.NotNull(regions);
        Assert.NotEmpty(regions);
    }

    [Fact]
    public async Task GetEstimate_PostgreSqlPlatform_ReturnsPricing()
    {
        var response = await _client.GetAsync(
            "/api/pricing/estimate?service=Azure+Database+for+PostgreSQL+-+Flexible+Server&compute=GP_Standard_D2s_v3&storageGb=32&platform=PostgreSql");

        response.EnsureSuccessStatusCode();
        var pricing = await response.Content.ReadFromJsonAsync<List<RegionPricing>>(_jsonOptions);
        Assert.NotNull(pricing);
        Assert.NotEmpty(pricing);
    }

    [Theory]
    [InlineData(DatabasePlatform.SqlServer)]
    [InlineData(DatabasePlatform.PostgreSql)]
    public async Task GetEstimate_AllPlatforms_ReturnsSortedPricing(DatabasePlatform platform)
    {
        var service = platform == DatabasePlatform.PostgreSql
            ? "Azure+Database+for+PostgreSQL+-+Flexible+Server"
            : "Azure+SQL+Database";
        var compute = platform == DatabasePlatform.PostgreSql
            ? "GP_Standard_D2s_v3"
            : "GP_Gen5_2";

        var response = await _client.GetAsync(
            $"/api/pricing/estimate?service={service}&compute={compute}&storageGb=32&platform={platform}");

        response.EnsureSuccessStatusCode();
        var pricing = await response.Content.ReadFromJsonAsync<List<RegionPricing>>(_jsonOptions);
        Assert.NotNull(pricing);
        Assert.True(pricing.Count > 1);
        for (int i = 1; i < pricing.Count; i++)
        {
            Assert.True(pricing[i].EstimatedMonthlyCostUsd >= pricing[i - 1].EstimatedMonthlyCostUsd);
        }
    }
}

public class PricingControllerAuthTests : IClassFixture<UnauthenticatedWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PricingControllerAuthTests(UnauthenticatedWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetRegions_Unauthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/pricing/regions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEstimate_Unauthenticated_Returns401()
    {
        var response = await _client.GetAsync(
            "/api/pricing/estimate?service=Azure+SQL+Database&compute=GP_Gen5_2&storageGb=32");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
