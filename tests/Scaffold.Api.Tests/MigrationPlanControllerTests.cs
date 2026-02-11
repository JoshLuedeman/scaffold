using System.Net;
using System.Net.Http.Json;
using Scaffold.Api.Controllers;
using Scaffold.Api.Dtos;
using Scaffold.Api.Tests.Infrastructure;
using Scaffold.Core.Enums;
using Scaffold.Core.Models;

namespace Scaffold.Api.Tests;

public class MigrationPlanControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MigrationPlanControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<MigrationProject> CreateProjectAsync()
    {
        var request = new CreateProjectRequest($"Plan-Test-{Guid.NewGuid()}", "desc", null);
        var response = await _client.PostAsJsonAsync("/api/projects", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MigrationProject>())!;
    }

    private async Task<MigrationPlanResponse> CreatePlanAsync(Guid projectId)
    {
        var request = new CreateMigrationPlanRequest(MigrationStrategy.Cutover);
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/migration-plans", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MigrationPlanResponse>())!;
    }

    [Fact]
    public async Task Create_ReturnsCreatedPlan()
    {
        var project = await CreateProjectAsync();

        var plan = await CreatePlanAsync(project.Id);

        Assert.NotNull(plan);
        Assert.Equal(project.Id, plan.ProjectId);
        Assert.Equal("Cutover", plan.Strategy);
    }

    [Fact]
    public async Task GetLatest_ReturnsPlan()
    {
        var project = await CreateProjectAsync();
        var created = await CreatePlanAsync(project.Id);

        var response = await _client.GetAsync(
            $"/api/projects/{project.Id}/migration-plans/latest");

        response.EnsureSuccessStatusCode();
        var plan = await response.Content.ReadFromJsonAsync<MigrationPlanResponse>();
        Assert.NotNull(plan);
        Assert.Equal(created.Id, plan.Id);
    }

    [Fact]
    public async Task Update_NonApprovedPlan_Succeeds()
    {
        var project = await CreateProjectAsync();
        var created = await CreatePlanAsync(project.Id);

        var updateRequest = new UpdateMigrationPlanRequest(
            Strategy: MigrationStrategy.ContinuousSync);

        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/migration-plans/{created.Id}", updateRequest);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<MigrationPlanResponse>();
        Assert.NotNull(updated);
        Assert.Equal("ContinuousSync", updated.Strategy);
    }

    [Fact]
    public async Task Update_ApprovedPlan_ReturnsBadRequest()
    {
        var project = await CreateProjectAsync();
        var created = await CreatePlanAsync(project.Id);

        // Approve the plan first
        var approveResponse = await _client.PostAsync(
            $"/api/projects/{project.Id}/migration-plans/{created.Id}/approve", null);
        approveResponse.EnsureSuccessStatusCode();

        // Try to update
        var updateRequest = new UpdateMigrationPlanRequest(
            Strategy: MigrationStrategy.ContinuousSync);
        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/migration-plans/{created.Id}", updateRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Approve_SetsIsApproved()
    {
        var project = await CreateProjectAsync();
        var created = await CreatePlanAsync(project.Id);

        var response = await _client.PostAsync(
            $"/api/projects/{project.Id}/migration-plans/{created.Id}/approve", null);

        response.EnsureSuccessStatusCode();
        var plan = await response.Content.ReadFromJsonAsync<MigrationPlanResponse>();
        Assert.NotNull(plan);
        Assert.True(plan.IsApproved);
        Assert.False(plan.IsRejected);
    }

    [Fact]
    public async Task Reject_SetsIsRejected()
    {
        var project = await CreateProjectAsync();
        var created = await CreatePlanAsync(project.Id);

        var rejectRequest = new RejectMigrationPlanRequest("Not ready");
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/migration-plans/{created.Id}/reject", rejectRequest);

        response.EnsureSuccessStatusCode();
        var plan = await response.Content.ReadFromJsonAsync<MigrationPlanResponse>();
        Assert.NotNull(plan);
        Assert.True(plan.IsRejected);
        Assert.Equal("Not ready", plan.RejectionReason);
        Assert.False(plan.IsApproved);
    }
}
