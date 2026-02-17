using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Scaffold.Api.Controllers;
using Scaffold.Api.Tests.Infrastructure;

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
}
