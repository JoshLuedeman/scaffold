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

    [Theory]
    [InlineData(null, "Empty body should return 400")]
    [InlineData("", "Empty name should return 400")]
    [InlineData("   ", "Whitespace-only name should return 400")]
    public async Task CreateProject_WithInvalidPayload_Returns400(string? name, string reason)
    {
        HttpResponseMessage response;

        if (name is null)
        {
            response = await _client.PostAsJsonAsync("/api/projects", (object?)null);
        }
        else
        {
            response = await _client.PostAsJsonAsync("/api/projects", new { Name = name });
        }

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProject_WithValidPayload_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = "Valid Project"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateProject_WithNameExceedingMaxLength_Returns400()
    {
        var longName = new string('A', 201);

        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = longName
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProject_WithDescriptionExceedingMaxLength_Returns400()
    {
        var longDescription = new string('A', 2001);

        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = "Valid Project",
            Description = longDescription
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProject_WithNameAtMaxLength_Returns201()
    {
        var maxName = new string('A', 200);

        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = maxName
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
