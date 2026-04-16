using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Scaffold.Core.Enums;
using Scaffold.Core.Models;
using Scaffold.Infrastructure.Data;

namespace Scaffold.Api.Tests;

public class ConcurrencyTests : IDisposable
{
    private readonly ScaffoldDbContext _context;

    public ConcurrencyTests()
    {
        var options = new DbContextOptionsBuilder<ScaffoldDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ScaffoldDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void MigrationProject_HasRowVersionProperty()
    {
        var property = typeof(MigrationProject).GetProperty("RowVersion");
        Assert.NotNull(property);
        Assert.Equal(typeof(byte[]), property!.PropertyType);
    }

    [Fact]
    public void MigrationProject_RowVersion_HasTimestampAttribute()
    {
        var property = typeof(MigrationProject).GetProperty("RowVersion")!;
        var attribute = property.GetCustomAttribute<TimestampAttribute>();
        Assert.NotNull(attribute);
    }

    [Fact]
    public void MigrationProject_RowVersion_IsInitializedToEmptyArray()
    {
        var project = new MigrationProject
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            CreatedBy = "user"
        };

        Assert.NotNull(project.RowVersion);
        Assert.Empty(project.RowVersion);
    }

    [Fact]
    public void MigrationPlan_HasRowVersionProperty()
    {
        var property = typeof(MigrationPlan).GetProperty("RowVersion");
        Assert.NotNull(property);
        Assert.Equal(typeof(byte[]), property!.PropertyType);
    }

    [Fact]
    public void MigrationPlan_RowVersion_HasTimestampAttribute()
    {
        var property = typeof(MigrationPlan).GetProperty("RowVersion")!;
        var attribute = property.GetCustomAttribute<TimestampAttribute>();
        Assert.NotNull(attribute);
    }

    [Fact]
    public void MigrationPlan_RowVersion_IsInitializedToEmptyArray()
    {
        var plan = new MigrationPlan
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Strategy = MigrationStrategy.Cutover,
            TargetTier = new TierRecommendation()
        };

        Assert.NotNull(plan.RowVersion);
        Assert.Empty(plan.RowVersion);
    }

    [Fact]
    public void DbContext_ConfiguresMigrationProject_RowVersionAsConcurrencyToken()
    {
        var entityType = _context.Model.FindEntityType(typeof(MigrationProject));
        Assert.NotNull(entityType);

        var rowVersionProperty = entityType!.FindProperty("RowVersion");
        Assert.NotNull(rowVersionProperty);
        Assert.True(rowVersionProperty!.IsConcurrencyToken,
            "RowVersion should be configured as a concurrency token on MigrationProject");
    }

    [Fact]
    public void DbContext_ConfiguresMigrationPlan_RowVersionAsConcurrencyToken()
    {
        var entityType = _context.Model.FindEntityType(typeof(MigrationPlan));
        Assert.NotNull(entityType);

        var rowVersionProperty = entityType!.FindProperty("RowVersion");
        Assert.NotNull(rowVersionProperty);
        Assert.True(rowVersionProperty!.IsConcurrencyToken,
            "RowVersion should be configured as a concurrency token on MigrationPlan");
    }

    [Fact]
    public async Task MigrationProject_ConcurrentUpdate_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange: the in-memory provider doesn't auto-increment rowversion,
        // so we simulate what SQL Server does by manually changing the stored
        // RowVersion via a separate context between read and write.
        var projectId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ScaffoldDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Seed with a known RowVersion
        using (var seedContext = new ScaffoldDbContext(options))
        {
            var project = new MigrationProject
            {
                Id = projectId,
                Name = "Original",
                CreatedBy = "user",
                RowVersion = [1, 0, 0, 0, 0, 0, 0, 1]
            };
            seedContext.MigrationProjects.Add(project);
            await seedContext.SaveChangesAsync();
        }

        // Context1 reads entity — captures original RowVersion [1,0,0,0,0,0,0,1]
        using var context1 = new ScaffoldDbContext(options);
        var project1 = await context1.MigrationProjects.FindAsync(projectId);
        Assert.NotNull(project1);

        // Another context simulates a concurrent update that increments RowVersion
        using (var context2 = new ScaffoldDbContext(options))
        {
            var project2 = await context2.MigrationProjects.FindAsync(projectId);
            project2!.Name = "Updated by context2";
            project2.RowVersion = [1, 0, 0, 0, 0, 0, 0, 2];
            await context2.SaveChangesAsync();
        }

        // Context1's original RowVersion is now stale — save should fail
        project1!.Name = "Updated by context1";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => context1.SaveChangesAsync());
    }

    [Fact]
    public async Task MigrationPlan_ConcurrentUpdate_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange: simulate concurrency conflict by manually changing the stored
        // RowVersion between read and write (in-memory provider doesn't auto-increment).
        var projectId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ScaffoldDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Seed with a known RowVersion
        using (var seedContext = new ScaffoldDbContext(options))
        {
            seedContext.MigrationProjects.Add(new MigrationProject
            {
                Id = projectId,
                Name = "Parent",
                CreatedBy = "user"
            });
            seedContext.MigrationPlans.Add(new MigrationPlan
            {
                Id = planId,
                ProjectId = projectId,
                Strategy = MigrationStrategy.Cutover,
                RowVersion = [1, 0, 0, 0, 0, 0, 0, 1],
                TargetTier = new TierRecommendation
                {
                    ServiceTier = "GP",
                    ComputeSize = "GP_Gen5_2",
                    StorageGb = 32
                }
            });
            await seedContext.SaveChangesAsync();
        }

        // Context1 reads entity — captures original RowVersion
        using var context1 = new ScaffoldDbContext(options);
        var plan1 = await context1.MigrationPlans.FindAsync(planId);
        Assert.NotNull(plan1);

        // Another context simulates a concurrent update that increments RowVersion
        using (var context2 = new ScaffoldDbContext(options))
        {
            var plan2 = await context2.MigrationPlans.FindAsync(planId);
            plan2!.IsApproved = true;
            plan2.ApprovedBy = "admin1";
            plan2.RowVersion = [1, 0, 0, 0, 0, 0, 0, 2];
            await context2.SaveChangesAsync();
        }

        // Context1's original RowVersion is now stale — save should fail
        plan1!.IsApproved = true;
        plan1.ApprovedBy = "admin2";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => context1.SaveChangesAsync());
    }
}
