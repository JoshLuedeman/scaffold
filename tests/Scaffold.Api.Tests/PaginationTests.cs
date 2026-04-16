using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Scaffold.Api.Controllers;
using Scaffold.Api.Tests.Infrastructure;
using Scaffold.Core.Models;

namespace Scaffold.Api.Tests;

public class PaginationTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public PaginationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<MigrationProject> CreateProjectAsync(string name)
    {
        var request = new CreateProjectRequest(name, $"Description for {name}", null);
        var response = await _client.PostAsJsonAsync("/api/projects", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MigrationProject>(_jsonOptions))!;
    }

    [Fact]
    public async Task GetAll_DefaultPagination_ReturnsFirstPage()
    {
        // Arrange — create a project so the list is non-empty
        await CreateProjectAsync("Pagination Default");

        // Act — call without query params (defaults: page=1, pageSize=25)
        var response = await _client.GetAsync("/api/projects");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<MigrationProject>>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Equal(1, result.Page);
        Assert.Equal(25, result.PageSize);
        Assert.True(result.TotalCount >= 1);
        Assert.True(result.Items.Count >= 1);
        Assert.True(result.Items.Count <= 25);
    }

    [Fact]
    public async Task GetAll_ExplicitPageAndSize_ReturnsPaginatedResult()
    {
        // Arrange — ensure at least 3 projects exist
        await CreateProjectAsync("Page Explicit A");
        await CreateProjectAsync("Page Explicit B");
        await CreateProjectAsync("Page Explicit C");

        // Act — request page 1 with pageSize 2
        var response = await _client.GetAsync("/api/projects?page=1&pageSize=2");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<MigrationProject>>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.True(result.Items.Count <= 2);
        Assert.True(result.TotalCount >= 3);
    }

    [Fact]
    public async Task GetAll_SecondPage_ReturnsCorrectItems()
    {
        // Arrange — ensure at least 3 projects
        await CreateProjectAsync("Second Page A");
        await CreateProjectAsync("Second Page B");
        await CreateProjectAsync("Second Page C");

        // Act — request page 2 with pageSize 2
        var page1Response = await _client.GetAsync("/api/projects?page=1&pageSize=2");
        var page2Response = await _client.GetAsync("/api/projects?page=2&pageSize=2");

        // Assert
        page1Response.EnsureSuccessStatusCode();
        page2Response.EnsureSuccessStatusCode();

        var page1 = await page1Response.Content.ReadFromJsonAsync<PaginatedResult<MigrationProject>>(_jsonOptions);
        var page2 = await page2Response.Content.ReadFromJsonAsync<PaginatedResult<MigrationProject>>(_jsonOptions);

        Assert.NotNull(page1);
        Assert.NotNull(page2);
        Assert.Equal(2, page1.Items.Count);
        Assert.True(page2.Items.Count >= 1);

        // Ensure pages contain different items
        var page1Ids = page1.Items.Select(p => p.Id).ToHashSet();
        var page2Ids = page2.Items.Select(p => p.Id).ToHashSet();
        Assert.Empty(page1Ids.Intersect(page2Ids));
    }

    [Fact]
    public async Task GetAll_HasNextPage_TrueWhenMoreItemsExist()
    {
        // Arrange — ensure at least 2 projects
        await CreateProjectAsync("HasNext A");
        await CreateProjectAsync("HasNext B");

        // Act
        var response = await _client.GetAsync("/api/projects?page=1&pageSize=1");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<MigrationProject>>(_jsonOptions);
        Assert.NotNull(result);
        Assert.True(result.HasNextPage);
    }

    [Fact]
    public async Task GetAll_HasPreviousPage_FalseOnFirstPage()
    {
        var response = await _client.GetAsync("/api/projects?page=1&pageSize=10");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<MigrationProject>>(_jsonOptions);
        Assert.NotNull(result);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public async Task GetAll_HasPreviousPage_TrueOnSecondPage()
    {
        // Arrange
        await CreateProjectAsync("HasPrev A");
        await CreateProjectAsync("HasPrev B");

        // Act
        var response = await _client.GetAsync("/api/projects?page=2&pageSize=1");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<MigrationProject>>(_jsonOptions);
        Assert.NotNull(result);
        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public async Task GetAll_TotalPages_CalculatedCorrectly()
    {
        // Arrange — ensure at least 5 projects
        for (int i = 0; i < 5; i++)
            await CreateProjectAsync($"TotalPages {i}");

        // Act
        var response = await _client.GetAsync("/api/projects?page=1&pageSize=2");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<MigrationProject>>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Equal((int)Math.Ceiling(result.TotalCount / 2.0), result.TotalPages);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GetAll_InvalidPage_ClampedToOne(int invalidPage)
    {
        var response = await _client.GetAsync($"/api/projects?page={invalidPage}");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<MigrationProject>>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Equal(1, result.Page);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(101, 100)]
    [InlineData(200, 100)]
    public async Task GetAll_InvalidPageSize_ClampedToValidRange(int invalidPageSize, int expectedPageSize)
    {
        var response = await _client.GetAsync($"/api/projects?pageSize={invalidPageSize}");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<MigrationProject>>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Equal(expectedPageSize, result.PageSize);
    }

    [Fact]
    public async Task GetAll_PageBeyondTotal_ReturnsEmptyItems()
    {
        // Act — request a very high page number
        var response = await _client.GetAsync("/api/projects?page=9999&pageSize=20");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<MigrationProject>>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(9999, result.Page);
    }

    [Fact]
    public async Task GetAll_PageSizeOne_ReturnsSingleItem()
    {
        // Arrange
        await CreateProjectAsync("Single Item");

        // Act
        var response = await _client.GetAsync("/api/projects?page=1&pageSize=1");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<MigrationProject>>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetAll_PageSizeMax_Returns100OrFewer()
    {
        var response = await _client.GetAsync("/api/projects?page=1&pageSize=100");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<MigrationProject>>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Equal(100, result.PageSize);
        Assert.True(result.Items.Count <= 100);
    }
}
