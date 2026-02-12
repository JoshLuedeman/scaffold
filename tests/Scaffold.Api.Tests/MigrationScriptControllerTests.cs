using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Scaffold.Api.Controllers;
using Scaffold.Api.Tests.Infrastructure;
using Scaffold.Core.Models;

namespace Scaffold.Api.Tests;

public class MigrationScriptControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public MigrationScriptControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<MigrationProject> CreateProjectAsync()
    {
        var request = new CreateProjectRequest($"Script-Test-{Guid.NewGuid()}", "desc", null);
        var response = await _client.PostAsJsonAsync("/api/projects", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MigrationProject>(_jsonOptions))!;
    }

    [Fact]
    public async Task GetAvailableScripts_NoAssessment_Returns404()
    {
        var project = await CreateProjectAsync();

        var response = await _client.GetAsync(
            $"/api/projects/{project.Id}/migration-scripts/available");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("No assessment found", body);
    }

    [Fact]
    public async Task PreviewScript_NoAssessment_Returns404()
    {
        var project = await CreateProjectAsync();

        var response = await _client.GetAsync(
            $"/api/projects/{project.Id}/migration-scripts/preview?scriptId=drop-foreign-keys");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("No assessment found", body);
    }

    [Fact]
    public async Task PreviewScript_MissingScriptId_Returns400()
    {
        var project = await CreateProjectAsync();

        var response = await _client.GetAsync(
            $"/api/projects/{project.Id}/migration-scripts/preview");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public class MigrationScriptControllerAuthTests : IClassFixture<UnauthenticatedWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MigrationScriptControllerAuthTests(UnauthenticatedWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAvailableScripts_Unauthenticated_Returns401()
    {
        var response = await _client.GetAsync(
            $"/api/projects/{Guid.NewGuid()}/migration-scripts/available");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PreviewScript_Unauthenticated_Returns401()
    {
        var response = await _client.GetAsync(
            $"/api/projects/{Guid.NewGuid()}/migration-scripts/preview?scriptId=drop-foreign-keys");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
