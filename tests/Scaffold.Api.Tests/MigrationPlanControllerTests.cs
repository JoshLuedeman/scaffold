using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scaffold.Api.Controllers;
using Scaffold.Api.Dtos;
using Scaffold.Api.Tests.Infrastructure;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;
using Scaffold.Infrastructure.Data;

namespace Scaffold.Api.Tests;

public class MigrationPlanControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MigrationPlanControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<MigrationProject> CreateProjectAsync()
    {
        var request = new CreateProjectRequest($"Plan-Test-{Guid.NewGuid()}", "desc", null);
        var response = await _client.PostAsJsonAsync("/api/projects", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MigrationProject>(_jsonOptions))!;
    }

    private async Task<MigrationPlanResponse> CreatePlanAsync(Guid projectId)
    {
        var request = new CreateMigrationPlanRequest(MigrationStrategy.Cutover);
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/migration-plans", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MigrationPlanResponse>(_jsonOptions))!;
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
        var plan = await response.Content.ReadFromJsonAsync<MigrationPlanResponse>(_jsonOptions);
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
        var updated = await response.Content.ReadFromJsonAsync<MigrationPlanResponse>(_jsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("ContinuousSync", updated.Strategy);
    }

    [Fact]
    public async Task Update_ApprovedPlan_ResetsApproval()
    {
        var project = await CreateProjectAsync();
        var created = await CreatePlanAsync(project.Id);

        // Approve the plan first
        var approveResponse = await _client.PostAsync(
            $"/api/projects/{project.Id}/migration-plans/{created.Id}/approve", null);
        approveResponse.EnsureSuccessStatusCode();

        // Update should succeed and reset approval
        var updateRequest = new UpdateMigrationPlanRequest(
            Strategy: MigrationStrategy.ContinuousSync);
        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/migration-plans/{created.Id}", updateRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<MigrationPlanResponse>(_jsonOptions);
        Assert.NotNull(updated);
        Assert.False(updated.IsApproved);
        Assert.Equal("ContinuousSync", updated.Strategy);
    }

    [Fact]
    public async Task Approve_SetsIsApproved()
    {
        var project = await CreateProjectAsync();
        var created = await CreatePlanAsync(project.Id);

        var response = await _client.PostAsync(
            $"/api/projects/{project.Id}/migration-plans/{created.Id}/approve", null);

        response.EnsureSuccessStatusCode();
        var plan = await response.Content.ReadFromJsonAsync<MigrationPlanResponse>(_jsonOptions);
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
        var plan = await response.Content.ReadFromJsonAsync<MigrationPlanResponse>(_jsonOptions);
        Assert.NotNull(plan);
        Assert.True(plan.IsRejected);
        Assert.Equal("Not ready", plan.RejectionReason);
        Assert.False(plan.IsApproved);
    }

    [Fact]
    public async Task Create_PopulatesSourceConnectionString_FromProjectConnection()
    {
        var project = new MigrationProject
        {
            Id = Guid.NewGuid(),
            Name = "ConnString Test",
            CreatedBy = "test",
            SourceConnection = new ConnectionInfo
            {
                Id = Guid.NewGuid(),
                Server = "myserver.database.windows.net",
                Database = "MyDb",
                Port = 1433,
                UseSqlAuthentication = false,
                TrustServerCertificate = true
            }
        };

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
            db.MigrationProjects.Add(project);
            await db.SaveChangesAsync();
        }

        var request = new CreateMigrationPlanRequest(MigrationStrategy.Cutover);
        var response = await _client.PostAsJsonAsync($"/api/projects/{project.Id}/migration-plans", request);
        response.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
            var protector = scope.ServiceProvider.GetRequiredService<IConnectionStringProtector>();
            var plan = await db.MigrationPlans.FirstOrDefaultAsync(p => p.ProjectId == project.Id);
            Assert.NotNull(plan);
            Assert.False(string.IsNullOrWhiteSpace(plan.SourceConnectionString));

            // The stored value should be encrypted (not plaintext)
            Assert.DoesNotContain("myserver.database.windows.net", plan.SourceConnectionString);

            // After unprotecting, the original connection string is recovered
            var decrypted = protector.Unprotect(plan.SourceConnectionString);
            Assert.Contains("myserver.database.windows.net", decrypted);
            Assert.Contains("MyDb", decrypted);
        }
    }
}