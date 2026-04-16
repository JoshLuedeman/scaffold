using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Scaffold.Api.Controllers;
using Scaffold.Api.Tests.Infrastructure;
using Scaffold.Core.Models;

namespace Scaffold.Api.Tests;

public class ProjectControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public ProjectControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/api/projects");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<MigrationProject>>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Create_ReturnsCreatedProject()
    {
        var request = new CreateProjectRequest("Test Project", "A test project", null);

        var response = await _client.PostAsJsonAsync("/api/projects", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<MigrationProject>(_jsonOptions);
        Assert.NotNull(project);
        Assert.Equal("Test Project", project.Name);
        Assert.Equal("A test project", project.Description);
        Assert.NotEqual(Guid.Empty, project.Id);
    }

    [Fact]
    public async Task GetById_ReturnsProject()
    {
        // Arrange: create a project first
        var request = new CreateProjectRequest("GetById Project", "desc", null);
        var createResponse = await _client.PostAsJsonAsync("/api/projects", request);
        var created = await createResponse.Content.ReadFromJsonAsync<MigrationProject>(_jsonOptions);

        // Act
        var response = await _client.GetAsync($"/api/projects/{created!.Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<MigrationProject>(_jsonOptions);
        Assert.NotNull(project);
        Assert.Equal(created.Id, project.Id);
        Assert.Equal("GetById Project", project.Name);
    }

    [Fact]
    public async Task GetById_Nonexistent_Returns404()
    {
        var response = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ReturnsUpdatedProject()
    {
        // Arrange
        var createRequest = new CreateProjectRequest("Original", "original desc", null);
        var createResponse = await _client.PostAsJsonAsync("/api/projects", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MigrationProject>(_jsonOptions);

        var updateRequest = new UpdateProjectRequest("Updated", "updated desc");

        // Act
        var response = await _client.PutAsJsonAsync($"/api/projects/{created!.Id}", updateRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<MigrationProject>(_jsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.Name);
        Assert.Equal("updated desc", updated.Description);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        // Arrange
        var request = new CreateProjectRequest("ToDelete", null, null);
        var createResponse = await _client.PostAsJsonAsync("/api/projects", request);
        var created = await createResponse.Content.ReadFromJsonAsync<MigrationProject>(_jsonOptions);

        // Act
        var response = await _client.DeleteAsync($"/api/projects/{created!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify it's gone
        var getResponse = await _client.GetAsync($"/api/projects/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}
