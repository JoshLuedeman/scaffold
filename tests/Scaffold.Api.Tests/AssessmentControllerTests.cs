using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Scaffold.Api.Controllers;
using Scaffold.Api.Tests.Infrastructure;
using Scaffold.Core.Models;
using Scaffold.Infrastructure.Data;

namespace Scaffold.Api.Tests;

public class AssessmentControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AssessmentControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<MigrationProject> CreateProjectAsync()
    {
        var request = new CreateProjectRequest($"Assess-Test-{Guid.NewGuid()}", "desc", null);
        var response = await _client.PostAsJsonAsync("/api/projects", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MigrationProject>(_jsonOptions))!;
    }

    private async Task<MigrationProject> CreateProjectWithConnectionAsync()
    {
        var project = new MigrationProject
        {
            Id = Guid.NewGuid(),
            Name = $"Assess-Conn-{Guid.NewGuid()}",
            CreatedBy = "test",
            SourceConnection = new ConnectionInfo
            {
                Id = Guid.NewGuid(),
                Server = "localhost",
                Database = "TestDb",
                Port = 1433,
                UseSqlAuthentication = true,
                Username = "sa",
                Password = "test",
                TrustServerCertificate = true
            }
        };

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
        db.MigrationProjects.Add(project);
        await db.SaveChangesAsync();

        return project;
    }

    // ── StartAssessment ─────────────────────────────────────────────

    [Fact]
    public async Task StartAssessment_WithConnectionInBody_CreatesConnectionAndReturnsReport()
    {
        var project = await CreateProjectAsync();

        var request = new AssessmentRequest(
            Server: "localhost",
            Database: "TestDb",
            Port: 1433,
            UseSqlAuthentication: true,
            Username: "sa",
            Password: "test",
            TrustServerCertificate: true,
            TargetService: null);

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/assessments", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<AssessmentReport>(_jsonOptions);
        Assert.NotNull(report);
        Assert.Equal(project.Id, report.ProjectId);
        Assert.True(report.Schema.TableCount > 0);
    }

    [Fact]
    public async Task StartAssessment_WithExistingConnection_ReturnsReport()
    {
        var project = await CreateProjectWithConnectionAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/assessments", (AssessmentRequest?)null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<AssessmentReport>(_jsonOptions);
        Assert.NotNull(report);
        Assert.Equal(project.Id, report.ProjectId);
    }

    [Fact]
    public async Task StartAssessment_NoConnectionAnywhere_ReturnsBadRequest()
    {
        var project = await CreateProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/assessments", (AssessmentRequest?)null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StartAssessment_NonexistentProject_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{Guid.NewGuid()}/assessments",
            new AssessmentRequest("s", "d", 1433, true, "u", "p", true, null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StartAssessment_MergesRuntimeCredentials()
    {
        var project = await CreateProjectWithConnectionAsync();

        var request = new AssessmentRequest(
            Server: "newserver",
            Database: "NewDb",
            Port: null,
            UseSqlAuthentication: null,
            Username: "newuser",
            Password: "newpass",
            TrustServerCertificate: null,
            TargetService: null);

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/assessments", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── GetLatest ───────────────────────────────────────────────────

    [Fact]
    public async Task GetLatest_AfterAssessment_ReturnsReport()
    {
        var project = await CreateProjectWithConnectionAsync();

        await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/assessments", (AssessmentRequest?)null);

        var response = await _client.GetAsync(
            $"/api/projects/{project.Id}/assessments/latest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<AssessmentReport>(_jsonOptions);
        Assert.NotNull(report);
    }

    [Fact]
    public async Task GetLatest_NoAssessment_Returns404()
    {
        var project = await CreateProjectAsync();

        var response = await _client.GetAsync(
            $"/api/projects/{project.Id}/assessments/latest");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetLatest_NonexistentProject_Returns404()
    {
        var response = await _client.GetAsync(
            $"/api/projects/{Guid.NewGuid()}/assessments/latest");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── EvaluateTarget ──────────────────────────────────────────────

    [Fact]
    public async Task EvaluateTarget_ReturnsScoreAndRisk()
    {
        var project = await CreateProjectWithConnectionAsync();

        await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/assessments", (AssessmentRequest?)null);

        var evalRequest = new EvaluateTargetRequest("Azure SQL Database");
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/assessments/evaluate-target", evalRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.True(body.TryGetProperty("compatibilityScore", out _));
        Assert.True(body.TryGetProperty("risk", out _));
    }

    [Fact]
    public async Task EvaluateTarget_NoAssessment_Returns404()
    {
        var project = await CreateProjectAsync();

        var evalRequest = new EvaluateTargetRequest("Azure SQL Database");
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/assessments/evaluate-target", evalRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── GetCompatibilitySummary ─────────────────────────────────────

    [Fact]
    public async Task GetCompatibilitySummary_ReturnsSummariesForAllServices()
    {
        var project = await CreateProjectWithConnectionAsync();

        await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/assessments", (AssessmentRequest?)null);

        var response = await _client.GetAsync(
            $"/api/projects/{project.Id}/assessments/compatibility-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summaries = await response.Content.ReadFromJsonAsync<List<JsonElement>>(_jsonOptions);
        Assert.NotNull(summaries);
        Assert.Equal(4, summaries.Count);
    }

    [Fact]
    public async Task GetCompatibilitySummary_NoAssessment_Returns404()
    {
        var project = await CreateProjectAsync();

        var response = await _client.GetAsync(
            $"/api/projects/{project.Id}/assessments/compatibility-summary");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── ReplacesExistingAssessment ──────────────────────────────────

    [Fact]
    public async Task StartAssessment_Twice_ReplacesExistingAssessment()
    {
        var project = await CreateProjectWithConnectionAsync();

        var r1 = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/assessments", (AssessmentRequest?)null);
        var report1 = await r1.Content.ReadFromJsonAsync<AssessmentReport>(_jsonOptions);

        var r2 = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/assessments", (AssessmentRequest?)null);
        var report2 = await r2.Content.ReadFromJsonAsync<AssessmentReport>(_jsonOptions);

        Assert.NotEqual(report1!.Id, report2!.Id);
    }
}
