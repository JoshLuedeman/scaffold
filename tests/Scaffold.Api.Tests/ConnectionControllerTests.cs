using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Scaffold.Api.Controllers;
using Scaffold.Api.Tests.Infrastructure;
using Scaffold.Core.Enums;

namespace Scaffold.Api.Tests;

public class ConnectionControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ConnectionControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task TestConnection_ReturnsSuccess()
    {
        var request = new ConnectionTestRequest(
            Server: "localhost",
            Database: "TestDb",
            Port: 1433,
            UseSqlAuthentication: true,
            Username: "sa",
            Password: "test",
            TrustServerCertificate: true);

        var response = await _client.PostAsJsonAsync("/api/connections/test", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task TestConnection_WithKeyVaultUri_ReturnsSuccess()
    {
        var request = new ConnectionTestRequest(
            Server: "myserver.database.windows.net",
            Database: "MyDb",
            KeyVaultSecretUri: "https://myvault.vault.azure.net/secrets/dbpass");

        var response = await _client.PostAsJsonAsync("/api/connections/test", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestConnection_PostgreSql_ReturnsSuccess()
    {
        var request = new ConnectionTestRequest(
            Server: "localhost",
            Database: "TestDb",
            Port: 5432,
            UseSqlAuthentication: true,
            Username: "postgres",
            Password: "test",
            Platform: DatabasePlatform.PostgreSql);

        var response = await _client.PostAsJsonAsync("/api/connections/test", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task TestConnection_PostgreSql_DefaultsSqlServerPort_StillRoutes()
    {
        // Verify that even with default port (1433), PostgreSQL platform routing works
        var request = new ConnectionTestRequest(
            Server: "localhost",
            Database: "TestDb",
            UseSqlAuthentication: true,
            Username: "postgres",
            Password: "test",
            Platform: DatabasePlatform.PostgreSql);

        var response = await _client.PostAsJsonAsync("/api/connections/test", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
    }

    [Theory]
    [InlineData(DatabasePlatform.SqlServer)]
    [InlineData(DatabasePlatform.PostgreSql)]
    public async Task TestConnection_AllPlatforms_RoutesThroughFactory(DatabasePlatform platform)
    {
        var request = new ConnectionTestRequest(
            Server: "localhost",
            Database: "TestDb",
            Port: platform == DatabasePlatform.PostgreSql ? 5432 : 1433,
            UseSqlAuthentication: true,
            Username: "testuser",
            Password: "test",
            Platform: platform);

        var response = await _client.PostAsJsonAsync("/api/connections/test", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
    }
}
