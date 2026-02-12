using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Scaffold.Api.Controllers;
using Scaffold.Api.Dtos;
using Scaffold.Api.Tests.Infrastructure;
using Scaffold.Core.Enums;
using Scaffold.Core.Models;
using Scaffold.Infrastructure.Data;

namespace Scaffold.Api.Tests;

public class MigrationControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MigrationControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<MigrationProject> CreateProjectAsync()
    {
        var request = new CreateProjectRequest($"Migration-Test-{Guid.NewGuid()}", "desc", null);
        var response = await _client.PostAsJsonAsync("/api/projects", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MigrationProject>(_jsonOptions))!;
    }

    private async Task<MigrationPlanResponse> CreateAndApprovePlanAsync(Guid projectId)
    {
        var planRequest = new CreateMigrationPlanRequest(MigrationStrategy.Cutover);
        var planResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/migration-plans", planRequest);
        planResponse.EnsureSuccessStatusCode();
        var plan = (await planResponse.Content.ReadFromJsonAsync<MigrationPlanResponse>(_jsonOptions))!;

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

    [Fact]
    public async Task Start_MissingSourceConnectionString_Returns400()
    {
        var projectId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
            var project = new MigrationProject
            {
                Id = projectId,
                Name = "No Source Test",
                CreatedBy = "test",
                Status = ProjectStatus.MigrationPlanned,
                MigrationPlan = new MigrationPlan
                {
                    Id = planId,
                    ProjectId = projectId,
                    Strategy = MigrationStrategy.Cutover,
                    IsApproved = true,
                    SourceConnectionString = null,
                    ExistingTargetConnectionString = "Server=target;Database=db;User Id=sa;Password=pass;"
                }
            };
            db.MigrationProjects.Add(project);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsync($"/api/projects/{projectId}/migrations/start", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Source connection string", body);
    }

    [Fact]
    public async Task Start_MissingTargetConnectionString_Returns400()
    {
        var projectId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
            var project = new MigrationProject
            {
                Id = projectId,
                Name = "No Target Test",
                CreatedBy = "test",
                Status = ProjectStatus.MigrationPlanned,
                MigrationPlan = new MigrationPlan
                {
                    Id = planId,
                    ProjectId = projectId,
                    Strategy = MigrationStrategy.Cutover,
                    IsApproved = true,
                    SourceConnectionString = "Server=source;Database=db;User Id=sa;Password=pass;",
                    ExistingTargetConnectionString = null
                }
            };
            db.MigrationProjects.Add(project);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsync($"/api/projects/{projectId}/migrations/start", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Target connection string", body);
    }
}
