using System.Net;
using Scaffold.Api.Tests.Infrastructure;

namespace Scaffold.Api.Tests;

public class AuthTests : IClassFixture<UnauthenticatedWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthTests(UnauthenticatedWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/projects")]
    [InlineData("/api/projects/00000000-0000-0000-0000-000000000001")]
    public async Task Unauthenticated_Requests_Return401(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
