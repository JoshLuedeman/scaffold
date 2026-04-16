using Microsoft.EntityFrameworkCore;
using Scaffold.Core.Enums;
using Scaffold.Core.Models;
using Scaffold.Infrastructure.Data;

namespace Scaffold.Api.Tests;

public class AuditTimestampTests : IDisposable
{
    private readonly ScaffoldDbContext _context;

    public AuditTimestampTests()
    {
        var options = new DbContextOptionsBuilder<ScaffoldDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ScaffoldDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreatingEntity_SetsCreatedAt()
    {
        var before = DateTime.UtcNow;

        var project = new MigrationProject
        {
            Id = Guid.NewGuid(),
            Name = "Timestamp Test",
            CreatedBy = "testuser"
        };

        _context.MigrationProjects.Add(project);
        await _context.SaveChangesAsync();

        var after = DateTime.UtcNow;

        Assert.True(project.CreatedAt >= before, "CreatedAt should be at or after the test start time");
        Assert.True(project.CreatedAt <= after, "CreatedAt should be at or before the test end time");
    }

    [Fact]
    public async Task CreatingEntity_SetsUpdatedAt()
    {
        var before = DateTime.UtcNow;

        var project = new MigrationProject
        {
            Id = Guid.NewGuid(),
            Name = "Timestamp Test",
            CreatedBy = "testuser"
        };

        _context.MigrationProjects.Add(project);
        await _context.SaveChangesAsync();

        var after = DateTime.UtcNow;

        Assert.True(project.UpdatedAt >= before, "UpdatedAt should be set on creation");
        Assert.True(project.UpdatedAt <= after, "UpdatedAt should be at or before the test end time");
        Assert.Equal(project.CreatedAt, project.UpdatedAt);
    }

    [Fact]
    public async Task UpdatingEntity_SetsUpdatedAt()
    {
        var project = new MigrationProject
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            CreatedBy = "testuser"
        };

        _context.MigrationProjects.Add(project);
        await _context.SaveChangesAsync();
        var originalUpdatedAt = project.UpdatedAt;

        await Task.Delay(50);

        project.Name = "Updated Name";
        _context.MigrationProjects.Update(project);
        await _context.SaveChangesAsync();

        Assert.True(project.UpdatedAt > originalUpdatedAt,
            $"UpdatedAt ({project.UpdatedAt:O}) should be later than original ({originalUpdatedAt:O})");
    }

    [Fact]
    public async Task UpdatingEntity_DoesNotChangeCreatedAt()
    {
        var project = new MigrationProject
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            CreatedBy = "testuser"
        };

        _context.MigrationProjects.Add(project);
        await _context.SaveChangesAsync();
        var originalCreatedAt = project.CreatedAt;

        await Task.Delay(50);

        project.Name = "Updated Name";
        _context.MigrationProjects.Update(project);
        await _context.SaveChangesAsync();

        Assert.Equal(originalCreatedAt, project.CreatedAt);
    }

    [Fact]
    public async Task MigrationPlan_InheritsAuditTimestamps()
    {
        var projectId = Guid.NewGuid();
        _context.MigrationProjects.Add(new MigrationProject
        {
            Id = projectId,
            Name = "Parent Project",
            CreatedBy = "testuser"
        });
        await _context.SaveChangesAsync();

        var before = DateTime.UtcNow;

        var plan = new MigrationPlan
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Strategy = MigrationStrategy.Cutover,
            TargetTier = new TierRecommendation
            {
                ServiceTier = "GP",
                ComputeSize = "GP_Gen5_2",
                StorageGb = 32
            }
        };

        _context.MigrationPlans.Add(plan);
        await _context.SaveChangesAsync();

        var after = DateTime.UtcNow;

        Assert.True(plan.CreatedAt >= before, "MigrationPlan CreatedAt should be set on creation");
        Assert.True(plan.CreatedAt <= after, "MigrationPlan CreatedAt should be at or before end time");
        Assert.True(plan.UpdatedAt >= before, "MigrationPlan UpdatedAt should be set on creation");
        Assert.Equal(plan.CreatedAt, plan.UpdatedAt);
    }

    [Fact]
    public async Task MigrationPlan_UpdatedAt_ChangesOnUpdate()
    {
        var projectId = Guid.NewGuid();
        _context.MigrationProjects.Add(new MigrationProject
        {
            Id = projectId,
            Name = "Parent Project",
            CreatedBy = "testuser"
        });
        await _context.SaveChangesAsync();

        var plan = new MigrationPlan
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Strategy = MigrationStrategy.Cutover,
            TargetTier = new TierRecommendation
            {
                ServiceTier = "GP",
                ComputeSize = "GP_Gen5_2",
                StorageGb = 32
            }
        };

        _context.MigrationPlans.Add(plan);
        await _context.SaveChangesAsync();
        var originalCreatedAt = plan.CreatedAt;
        var originalUpdatedAt = plan.UpdatedAt;

        await Task.Delay(50);

        plan.Strategy = MigrationStrategy.ContinuousSync;
        _context.MigrationPlans.Update(plan);
        await _context.SaveChangesAsync();

        Assert.Equal(originalCreatedAt, plan.CreatedAt);
        Assert.True(plan.UpdatedAt > originalUpdatedAt,
            "MigrationPlan UpdatedAt should change after update");
    }
}