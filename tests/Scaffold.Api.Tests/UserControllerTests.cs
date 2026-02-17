using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Scaffold.Api.Tests.Infrastructure;

namespace Scaffold.Api.Tests;

public class UserControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UserControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMe_ReturnsAuthenticatedUser()
    {
        var response = await _client.GetAsync("/api/user/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isAuthenticated").GetBoolean());
    }

    [Fact]
    public async Task GetMe_Unauthenticated_Returns401()
    {
        await using var factory = new UnauthenticatedWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/user/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
