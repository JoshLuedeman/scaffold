using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Scaffold.Api.Services;
using Scaffold.Api.Tests.Infrastructure;
using Scaffold.Core.Enums;
using Scaffold.Core.Models;
using Scaffold.Infrastructure.Data;

namespace Scaffold.Api.Tests;

public class MigrationControllerCancelTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MigrationControllerCancelTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Cancel_WhenMigrationIsNotRunning_Returns400()
    {
        var projectId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var migrationId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
            var project = new MigrationProject
            {
                Id = projectId,
                Name = "Cancel Not Running Test",
                CreatedBy = "test",
                Status = ProjectStatus.MigrationComplete,
                MigrationPlan = new MigrationPlan
                {
                    Id = planId,
                    ProjectId = projectId,
                    Strategy = MigrationStrategy.Cutover,
                    Status = MigrationStatus.Completed, // Not running
                    MigrationId = migrationId,
                    SourceConnectionString = "Server=source;",
                    ExistingTargetConnectionString = "Server=target;"
                }
            };
            db.MigrationProjects.Add(project);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsync(
            $"/api/projects/{projectId}/migrations/{migrationId}/cancel", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Only running migrations", body);
    }

    [Fact]
    public async Task Cancel_WhenMigrationIdDoesNotMatch_Returns400()
    {
        var projectId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var actualMigrationId = Guid.NewGuid();
        var wrongMigrationId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
            var project = new MigrationProject
            {
                Id = projectId,
                Name = "Cancel Wrong ID Test",
                CreatedBy = "test",
                Status = ProjectStatus.Migrating,
                MigrationPlan = new MigrationPlan
                {
                    Id = planId,
                    ProjectId = projectId,
                    Strategy = MigrationStrategy.Cutover,
                    Status = MigrationStatus.Running,
                    MigrationId = actualMigrationId,
                    SourceConnectionString = "Server=source;",
                    ExistingTargetConnectionString = "Server=target;"
                }
            };
            db.MigrationProjects.Add(project);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsync(
            $"/api/projects/{projectId}/migrations/{wrongMigrationId}/cancel", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("does not match", body);
    }

    [Fact]
    public async Task Cancel_WhenProjectNotFound_Returns404()
    {
        var nonExistentProjectId = Guid.NewGuid();
        var migrationId = Guid.NewGuid();

        var response = await _client.PostAsync(
            $"/api/projects/{nonExistentProjectId}/migrations/{migrationId}/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_WhenNoPlan_Returns404()
    {
        var projectId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
            var project = new MigrationProject
            {
                Id = projectId,
                Name = "Cancel No Plan Test",
                CreatedBy = "test",
                Status = ProjectStatus.Created,
                MigrationPlan = null
            };
            db.MigrationProjects.Add(project);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsync(
            $"/api/projects/{projectId}/migrations/{Guid.NewGuid()}/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
