using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Scaffold.Api.Services;
using Scaffold.Api.Tests.Infrastructure;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;
using Scaffold.Infrastructure.Data;

namespace Scaffold.Api.Tests;

public class MigrationSchedulerServiceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MigrationSchedulerServiceTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PollForScheduledMigrations_PicksUpDueMigrations()
    {
        // Arrange — create a factory with a shared DB name so we can seed and poll
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<ScaffoldDbContext>(o => o.UseInMemoryDatabase(dbName));

        // Add stub services needed by the scheduler
        services.AddScoped<IMigrationEngineFactory, StubMigrationEngineFactory>();
        services.AddScoped<Core.Interfaces.IProjectRepository>(sp =>
        {
            var db = sp.GetRequiredService<ScaffoldDbContext>();
            return new Scaffold.Infrastructure.Repositories.ProjectRepository(db);
        });
        services.AddScoped<MigrationProgressService>();
        services.AddSingleton<Migration.SqlServer.ValidationEngine>();
        services.AddSignalR();

        var provider = services.BuildServiceProvider();

        // Seed a project + scheduled plan
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
            var project = new MigrationProject
            {
                Id = Guid.NewGuid(),
                Name = "SchedulerTest",
                CreatedBy = "test",
                Status = ProjectStatus.MigrationPlanned,
                SourceConnection = new ConnectionInfo
                {
                    Id = Guid.NewGuid(),
                    Server = "localhost",
                    Database = "TestDB",
                    UseSqlAuthentication = true
                },
                MigrationPlan = new MigrationPlan
                {
                    Id = Guid.NewGuid(),
                    Strategy = MigrationStrategy.Cutover,
                    IsApproved = true,
                    Status = MigrationStatus.Scheduled,
                    ScheduledAt = DateTime.UtcNow.AddMinutes(-5),
                    SourceConnectionString = "Server=localhost;Database=Source;TrustServerCertificate=True",
                    ExistingTargetConnectionString = "Server=localhost;Database=Target;TrustServerCertificate=True"
                }
            };

            db.MigrationProjects.Add(project);
            await db.SaveChangesAsync();
        }

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var scheduler = new MigrationSchedulerService(scopeFactory, NullLogger<MigrationSchedulerService>.Instance);

        // Act
        await scheduler.PollForScheduledMigrationsAsync(CancellationToken.None);

        // Assert — plan should no longer be Scheduled (either Completed, Running, or Failed)
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
            var plan = await db.MigrationPlans.FirstAsync();
            Assert.NotEqual(MigrationStatus.Scheduled, plan.Status);
        }
    }

    [Fact]
    public async Task PollForScheduledMigrations_IgnoresFutureSchedules()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<ScaffoldDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<IMigrationEngineFactory, StubMigrationEngineFactory>();
        services.AddScoped<Core.Interfaces.IProjectRepository>(sp =>
            new Scaffold.Infrastructure.Repositories.ProjectRepository(
                sp.GetRequiredService<ScaffoldDbContext>()));
        services.AddScoped<MigrationProgressService>();
        services.AddSingleton<Migration.SqlServer.ValidationEngine>();
        services.AddSignalR();

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
            var project = new MigrationProject
            {
                Id = Guid.NewGuid(),
                Name = "FutureTest",
                CreatedBy = "test",
                Status = ProjectStatus.MigrationPlanned,
                SourceConnection = new ConnectionInfo
                {
                    Id = Guid.NewGuid(),
                    Server = "localhost",
                    Database = "TestDB",
                    UseSqlAuthentication = true
                },
                MigrationPlan = new MigrationPlan
                {
                    Id = Guid.NewGuid(),
                    Strategy = MigrationStrategy.Cutover,
                    IsApproved = true,
                    Status = MigrationStatus.Scheduled,
                    ScheduledAt = DateTime.UtcNow.AddHours(2),
                    SourceConnectionString = "Server=localhost;Database=Source;TrustServerCertificate=True",
                    ExistingTargetConnectionString = "Server=localhost;Database=Target;TrustServerCertificate=True"
                }
            };

            db.MigrationProjects.Add(project);
            await db.SaveChangesAsync();
        }

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var scheduler = new MigrationSchedulerService(scopeFactory, NullLogger<MigrationSchedulerService>.Instance);

        // Act
        await scheduler.PollForScheduledMigrationsAsync(CancellationToken.None);

        // Assert — plan should still be Scheduled (not yet due)
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
            var plan = await db.MigrationPlans.FirstAsync();
            Assert.Equal(MigrationStatus.Scheduled, plan.Status);
        }
    }

    [Fact]
    public async Task PollForScheduledMigrations_SkipsMissingConnectionStrings()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<ScaffoldDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<IMigrationEngineFactory, StubMigrationEngineFactory>();
        services.AddScoped<Core.Interfaces.IProjectRepository>(sp =>
            new Scaffold.Infrastructure.Repositories.ProjectRepository(
                sp.GetRequiredService<ScaffoldDbContext>()));
        services.AddScoped<MigrationProgressService>();
        services.AddSingleton<Migration.SqlServer.ValidationEngine>();
        services.AddSignalR();

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
            var project = new MigrationProject
            {
                Id = Guid.NewGuid(),
                Name = "NoConnTest",
                CreatedBy = "test",
                Status = ProjectStatus.MigrationPlanned,
                SourceConnection = new ConnectionInfo
                {
                    Id = Guid.NewGuid(),
                    Server = "localhost",
                    Database = "TestDB",
                    UseSqlAuthentication = true
                },
                MigrationPlan = new MigrationPlan
                {
                    Id = Guid.NewGuid(),
                    Strategy = MigrationStrategy.Cutover,
                    IsApproved = true,
                    Status = MigrationStatus.Scheduled,
                    ScheduledAt = DateTime.UtcNow.AddMinutes(-1),
                    // No connection strings set
                }
            };

            db.MigrationProjects.Add(project);
            await db.SaveChangesAsync();
        }

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var scheduler = new MigrationSchedulerService(scopeFactory, NullLogger<MigrationSchedulerService>.Instance);

        // Act
        await scheduler.PollForScheduledMigrationsAsync(CancellationToken.None);

        // Assert — plan should be Failed (missing connection strings)
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
            var plan = await db.MigrationPlans.FirstAsync();
            Assert.Equal(MigrationStatus.Failed, plan.Status);
        }
    }
}

public class MigrationProgressPersistenceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MigrationProgressPersistenceTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProgressEndpoint_ReturnsPersistedRecords()
    {
        var client = _factory.CreateClient();

        // Create a project
        var projectResponse = await client.PostAsJsonAsync("/api/projects", new
        {
            Name = "ProgressTest",
            Description = "Test progress persistence"
        });
        var projectJson = await projectResponse.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = projectJson.GetProperty("id").GetGuid();

        // Seed progress records directly in DB
        var migrationId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
            db.MigrationProgressRecords.AddRange(
                new MigrationProgressRecord
                {
                    Id = Guid.NewGuid(),
                    MigrationId = migrationId,
                    Phase = "Schema",
                    PercentComplete = 50,
                    CurrentTable = "dbo.Users",
                    RowsProcessed = 0,
                    Timestamp = DateTime.UtcNow.AddSeconds(-10)
                },
                new MigrationProgressRecord
                {
                    Id = Guid.NewGuid(),
                    MigrationId = migrationId,
                    Phase = "Data",
                    PercentComplete = 100,
                    CurrentTable = "dbo.Orders",
                    RowsProcessed = 5000,
                    Timestamp = DateTime.UtcNow
                });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync($"/api/projects/{projectId}/migrations/{migrationId}/progress");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var records = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(records);
        Assert.Equal(2, records!.Count);
        Assert.Equal("Schema", records[0].GetProperty("phase").GetString());
        Assert.Equal("Data", records[1].GetProperty("phase").GetString());
    }
}
