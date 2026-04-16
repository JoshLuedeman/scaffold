using System.Net;
using System.Text.Json;
using Scaffold.Api.Tests.Infrastructure;

namespace Scaffold.Api.Tests;

public class HealthCheckTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthCheckTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task HealthCheck_ReturnsJsonContentType()
    {
        var response = await _client.GetAsync("/healthz");

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task HealthCheck_IncludesDatabaseCheck()
    {
        var response = await _client.GetAsync("/healthz");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var checks = doc.RootElement.GetProperty("checks");
        Assert.True(checks.GetArrayLength() > 0);

        var hasDbCheck = false;
        foreach (var check in checks.EnumerateArray())
        {
            if (check.GetProperty("name").GetString() == "database")
            {
                hasDbCheck = true;
                Assert.Equal("Healthy", check.GetProperty("status").GetString());
                break;
            }
        }
        Assert.True(hasDbCheck, "Expected a health check named 'database'");
    }

    [Fact]
    public async Task HealthCheck_DoesNotRequireAuthentication()
    {
        using var factory = new UnauthenticatedWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthCheck_IncludesTotalDuration()
    {
        var response = await _client.GetAsync("/healthz");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("totalDuration", out var duration));
        Assert.True(duration.GetDouble() >= 0);
    }
}