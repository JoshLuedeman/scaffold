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

public class MigrationPlanSummaryTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MigrationPlanSummaryTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<MigrationProject> CreateProjectWithAssessmentAsync()
    {
        // Create via API first so the project goes through normal flow
        var createRequest = new CreateProjectRequest($"Summary-Test-{Guid.NewGuid()}", "desc", null);
        var createResponse = await _client.PostAsJsonAsync("/api/projects", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var project = (await createResponse.Content.ReadFromJsonAsync<MigrationProject>(_jsonOptions))!;

        // Seed assessment and connection directly into the shared DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
        var dbProject = await db.MigrationProjects.FindAsync(project.Id);

        var connection = new ConnectionInfo
        {
            Id = Guid.NewGuid(),
            Server = "localhost",
            Database = "TestDb",
            Port = 1433,
            UseSqlAuthentication = false,
            TrustServerCertificate = true
        };
        db.ConnectionInfos.Add(connection);
        dbProject!.SourceConnection = connection;

        var assessment = new AssessmentReport
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Schema = new SchemaInventory
            {
                TableCount = 5, ViewCount = 2, StoredProcedureCount = 3,
                Objects = [
                    new SchemaObject { Name = "Users", ObjectType = "Table" },
                    new SchemaObject { Name = "Orders", ObjectType = "Table" }
                ]
            },
            DataProfile = new DataProfile { TotalRowCount = 1000, TotalSizeBytes = 1_048_576 },
            CompatibilityIssues = [],
            CompatibilityScore = 100,
            Recommendation = new TierRecommendation
            {
                ServiceTier = "General Purpose",
                ComputeSize = "GP_Gen5_2",
                VCores = 2,
                StorageGb = 32,
                EstimatedMonthlyCostUsd = 150m,
                Reasoning = "Test"
            }
        };
        db.AssessmentReports.Add(assessment);
        dbProject.Assessment = assessment;

        await db.SaveChangesAsync();

        return dbProject;
    }

    private async Task<MigrationPlanResponse> CreatePlanAsync(Guid projectId, MigrationStrategy strategy = MigrationStrategy.Cutover)
    {
        var request = new CreateMigrationPlanRequest(strategy);
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/migration-plans", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MigrationPlanResponse>(_jsonOptions))!;
    }

    // ── GetSummary ──────────────────────────────────────────────────

    [Fact]
    public async Task GetSummary_ReturnsSummaryWithAllFields()
    {
        var project = await CreateProjectWithAssessmentAsync();
        var plan = await CreatePlanAsync(project.Id);

        var response = await _client.GetAsync(
            $"/api/projects/{project.Id}/migration-plans/{plan.Id}/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal(plan.Id.ToString(), summary.GetProperty("planId").GetString());
        Assert.True(summary.TryGetProperty("sourceDatabase", out _));
        Assert.True(summary.TryGetProperty("targetTier", out _));
        Assert.True(summary.TryGetProperty("objectCounts", out _));
        Assert.True(summary.TryGetProperty("estimatedTimeline", out _));
        Assert.True(summary.TryGetProperty("strategyDescription", out _));
    }

    [Fact]
    public async Task GetSummary_ContinuousSync_HasCorrectDescription()
    {
        var project = await CreateProjectWithAssessmentAsync();
        var plan = await CreatePlanAsync(project.Id, MigrationStrategy.ContinuousSync);

        var response = await _client.GetAsync(
            $"/api/projects/{project.Id}/migration-plans/{plan.Id}/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var desc = summary.GetProperty("strategyDescription").GetString();
        Assert.Contains("Continuous sync", desc);
    }

    [Fact]
    public async Task GetSummary_NonexistentPlan_Returns404()
    {
        var project = await CreateProjectWithAssessmentAsync();

        var response = await _client.GetAsync(
            $"/api/projects/{project.Id}/migration-plans/{Guid.NewGuid()}/summary");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── ValidateStart ───────────────────────────────────────────────

    [Fact]
    public async Task ValidateStart_ApprovedPlan_ReturnsCanStart()
    {
        var project = await CreateProjectWithAssessmentAsync();
        var plan = await CreatePlanAsync(project.Id);

        await _client.PostAsync(
            $"/api/projects/{project.Id}/migration-plans/{plan.Id}/approve", null);

        var response = await _client.PostAsync(
            $"/api/projects/{project.Id}/migration-plans/{plan.Id}/validate-start", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("canStart").GetBoolean());
    }

    [Fact]
    public async Task ValidateStart_UnapprovedPlan_ReturnsBadRequest()
    {
        var project = await CreateProjectWithAssessmentAsync();
        var plan = await CreatePlanAsync(project.Id);

        var response = await _client.PostAsync(
            $"/api/projects/{project.Id}/migration-plans/{plan.Id}/validate-start", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ValidateStart_RejectedPlan_ReturnsBadRequest()
    {
        var project = await CreateProjectWithAssessmentAsync();
        var plan = await CreatePlanAsync(project.Id);

        await _client.PostAsync(
            $"/api/projects/{project.Id}/migration-plans/{plan.Id}/approve", null);
        await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/migration-plans/{plan.Id}/reject",
            new RejectMigrationPlanRequest("No"));

        var response = await _client.PostAsync(
            $"/api/projects/{project.Id}/migration-plans/{plan.Id}/validate-start", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ValidateStart_NonexistentPlan_Returns404()
    {
        var project = await CreateProjectWithAssessmentAsync();

        var response = await _client.PostAsync(
            $"/api/projects/{project.Id}/migration-plans/{Guid.NewGuid()}/validate-start", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Approve already approved ────────────────────────────────────

    [Fact]
    public async Task Approve_AlreadyApproved_ReturnsBadRequest()
    {
        var project = await CreateProjectWithAssessmentAsync();
        var plan = await CreatePlanAsync(project.Id);

        await _client.PostAsync(
            $"/api/projects/{project.Id}/migration-plans/{plan.Id}/approve", null);

        var response = await _client.PostAsync(
            $"/api/projects/{project.Id}/migration-plans/{plan.Id}/approve", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Reject already rejected ─────────────────────────────────────

    [Fact]
    public async Task Reject_AlreadyRejected_ReturnsBadRequest()
    {
        var project = await CreateProjectWithAssessmentAsync();
        var plan = await CreatePlanAsync(project.Id);

        await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/migration-plans/{plan.Id}/reject",
            new RejectMigrationPlanRequest("First"));

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/migration-plans/{plan.Id}/reject",
            new RejectMigrationPlanRequest("Second"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Approve with schedule sets status to Scheduled ──────────────

    [Fact]
    public async Task Approve_WithSchedule_SetsStatusToScheduled()
    {
        var project = await CreateProjectWithAssessmentAsync();
        var request = new CreateMigrationPlanRequest(
            MigrationStrategy.Cutover,
            ScheduledAt: DateTime.UtcNow.AddHours(1));
        var createResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/migration-plans", request);
        var plan = (await createResponse.Content.ReadFromJsonAsync<MigrationPlanResponse>(_jsonOptions))!;

        var response = await _client.PostAsync(
            $"/api/projects/{project.Id}/migration-plans/{plan.Id}/approve", null);

        response.EnsureSuccessStatusCode();
        var approved = await response.Content.ReadFromJsonAsync<MigrationPlanResponse>(_jsonOptions);
        Assert.Equal("Scheduled", approved!.Status);
    }
}
