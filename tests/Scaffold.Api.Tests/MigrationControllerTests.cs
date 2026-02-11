using System.Net;
using System.Net.Http.Json;
using Scaffold.Api.Controllers;
using Scaffold.Api.Dtos;
using Scaffold.Api.Tests.Infrastructure;
using Scaffold.Core.Enums;
using Scaffold.Core.Models;

namespace Scaffold.Api.Tests;

public class MigrationControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MigrationControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<MigrationProject> CreateProjectAsync()
    {
        var request = new CreateProjectRequest($"Migration-Test-{Guid.NewGuid()}", "desc", null);
        var response = await _client.PostAsJsonAsync("/api/projects", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MigrationProject>())!;
    }

    private async Task<MigrationPlanResponse> CreateAndApprovePlanAsync(Guid projectId)
    {
        var planRequest = new CreateMigrationPlanRequest(MigrationStrategy.Cutover);
        var planResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/migration-plans", planRequest);
        planResponse.EnsureSuccessStatusCode();
        var plan = (await planResponse.Content.ReadFromJsonAsync<MigrationPlanResponse>())!;

        var approveResponse = await _client.PostAsync(
            $"/api/projects/{projectId}/migration-plans/{plan.Id}/approve", null);
        approveResponse.EnsureSuccessStatusCode();

        return plan;
    }

    [Fact]
    public async Task Start_WithoutApprovedPlan_ReturnsBadRequest()
    {
        var project = await CreateProjectAsync();

        // Create plan but don't approve it
        var planRequest = new CreateMigrationPlanRequest(MigrationStrategy.Cutover);
        var planResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/migration-plans", planRequest);
        planResponse.EnsureSuccessStatusCode();

        // Try to start migration
        var response = await _client.PostAsync(
            $"/api/projects/{project.Id}/migrations/start", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetMigration_Nonexistent_Returns404()
    {
        var project = await CreateProjectAsync();

        var response = await _client.GetAsync(
            $"/api/projects/{project.Id}/migrations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
