using System.Net;
using System.Net.Http.Json;
using Scaffold.Api.Tests.Infrastructure;

namespace Scaffold.Api.Tests;

public class RequestValidationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RequestValidationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateProject_EmptyName_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/projects",
            new { Name = "", Description = "test" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProject_NameTooLong_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/projects",
            new { Name = new string('x', 201), Description = "test" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProject_ValidName_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/projects",
            new { Name = "Test Project", Description = "test" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task TestConnection_MissingServer_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/connections/test",
            new { Server = "", Database = "testdb" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99999)]
    public async Task TestConnection_InvalidPort_Returns400(int port)
    {
        var response = await _client.PostAsJsonAsync("/api/connections/test",
            new { Server = "localhost", Database = "testdb", Port = port });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
