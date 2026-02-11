using Microsoft.EntityFrameworkCore;
using Scaffold.Core.Enums;
using Scaffold.Core.Models;
using Scaffold.Infrastructure.Data;

namespace Scaffold.Api.Tests.Infrastructure;

public class DbContextTests : IDisposable
{
    private readonly ScaffoldDbContext _context;

    public DbContextTests()
    {
        var options = new DbContextOptionsBuilder<ScaffoldDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ScaffoldDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task MigrationProject_CanSaveAndRetrieve_WithAllProperties()
    {
        var project = new MigrationProject
        {
            Id = Guid.NewGuid(),
            Name = "Test Project",
            Description = "A test description",
            Status = ProjectStatus.Assessed,
            CreatedBy = "user@test.com",
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        };

        _context.MigrationProjects.Add(project);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var retrieved = await _context.MigrationProjects.FindAsync(project.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(project.Id, retrieved.Id);
        Assert.Equal("Test Project", retrieved.Name);
        Assert.Equal("A test description", retrieved.Description);
        Assert.Equal(ProjectStatus.Assessed, retrieved.Status);
        Assert.Equal("user@test.com", retrieved.CreatedBy);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), retrieved.CreatedAt);
        Assert.Equal(new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), retrieved.UpdatedAt);
    }

    [Fact]
    public async Task ConnectionInfo_SavesCorrectly_WithNullableFields()
    {
        var projectId = Guid.NewGuid();
        var project = new MigrationProject
        {
            Id = projectId,
            Name = "Conn Test",
            CreatedBy = "user",
            SourceConnection = new ConnectionInfo
            {
                Id = Guid.NewGuid(),
                Server = "myserver.database.windows.net",
                Database = "mydb",
                Port = 1433,
                UseSqlAuthentication = true,
                Username = "admin",
                KeyVaultSecretUri = null,
                TrustServerCertificate = false
            }
        };

        _context.MigrationProjects.Add(project);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var retrieved = await _context.MigrationProjects
            .Include(p => p.SourceConnection)
            .FirstAsync(p => p.Id == projectId);

        Assert.NotNull(retrieved.SourceConnection);
        Assert.Equal("myserver.database.windows.net", retrieved.SourceConnection.Server);
        Assert.Equal("mydb", retrieved.SourceConnection.Database);
        Assert.Equal(1433, retrieved.SourceConnection.Port);
        Assert.True(retrieved.SourceConnection.UseSqlAuthentication);
        Assert.Equal("admin", retrieved.SourceConnection.Username);
        Assert.Null(retrieved.SourceConnection.KeyVaultSecretUri);
        Assert.False(retrieved.SourceConnection.TrustServerCertificate);
    }

    [Fact]
    public async Task ConnectionInfo_SavesCorrectly_AllNullableFieldsNull()
    {
        var projectId = Guid.NewGuid();
        var project = new MigrationProject
        {
            Id = projectId,
            Name = "Null Fields Test",
            CreatedBy = "user",
            SourceConnection = new ConnectionInfo
            {
                Id = Guid.NewGuid(),
                Server = "server",
                Database = "db",
                Username = null,
                KeyVaultSecretUri = null
            }
        };

        _context.MigrationProjects.Add(project);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var retrieved = await _context.MigrationProjects
            .Include(p => p.SourceConnection)
            .FirstAsync(p => p.Id == projectId);

        Assert.NotNull(retrieved.SourceConnection);
        Assert.Null(retrieved.SourceConnection.Username);
        Assert.Null(retrieved.SourceConnection.KeyVaultSecretUri);
    }

    [Fact]
    public async Task MigrationPlan_JsonConversion_IncludedAndExcludedObjects()
    {
        var projectId = Guid.NewGuid();
        _context.MigrationProjects.Add(new MigrationProject { Id = projectId, Name = "Plan Test", CreatedBy = "user" });
        await _context.SaveChangesAsync();

        var plan = new MigrationPlan
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Strategy = MigrationStrategy.Cutover,
            IncludedObjects = ["dbo.Users", "dbo.Orders", "dbo.Products"],
            ExcludedObjects = ["dbo.Logs", "dbo.TempData"],
            TargetTier = new TierRecommendation { ServiceTier = "GP", ComputeSize = "GP_Gen5_2", StorageGb = 32 }
        };

        _context.MigrationPlans.Add(plan);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var retrieved = await _context.MigrationPlans.FirstAsync(p => p.Id == plan.Id);

        Assert.Equal(3, retrieved.IncludedObjects.Count);
        Assert.Contains("dbo.Users", retrieved.IncludedObjects);
        Assert.Contains("dbo.Orders", retrieved.IncludedObjects);
        Assert.Contains("dbo.Products", retrieved.IncludedObjects);
        Assert.Equal(2, retrieved.ExcludedObjects.Count);
        Assert.Contains("dbo.Logs", retrieved.ExcludedObjects);
        Assert.Contains("dbo.TempData", retrieved.ExcludedObjects);
    }

    [Fact]
    public async Task MigrationPlan_TargetTier_OwnsOne_SavesAndLoads()
    {
        var projectId = Guid.NewGuid();
        _context.MigrationProjects.Add(new MigrationProject { Id = projectId, Name = "Tier Test", CreatedBy = "user" });
        await _context.SaveChangesAsync();

        var plan = new MigrationPlan
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Strategy = MigrationStrategy.ContinuousSync,
            TargetTier = new TierRecommendation
            {
                ServiceTier = "BusinessCritical",
                ComputeSize = "BC_Gen5_4",
                VCores = 4,
                Dtus = null,
                StorageGb = 64,
                EstimatedMonthlyCostUsd = 500.50m,
                Reasoning = "High availability needed"
            }
        };

        _context.MigrationPlans.Add(plan);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var retrieved = await _context.MigrationPlans.FirstAsync(p => p.Id == plan.Id);

        Assert.NotNull(retrieved.TargetTier);
        Assert.Equal("BusinessCritical", retrieved.TargetTier.ServiceTier);
        Assert.Equal("BC_Gen5_4", retrieved.TargetTier.ComputeSize);
        Assert.Equal(4, retrieved.TargetTier.VCores);
        Assert.Null(retrieved.TargetTier.Dtus);
        Assert.Equal(64, retrieved.TargetTier.StorageGb);
        Assert.Equal(500.50m, retrieved.TargetTier.EstimatedMonthlyCostUsd);
        Assert.Equal("High availability needed", retrieved.TargetTier.Reasoning);
    }

    [Fact]
    public async Task AssessmentReport_JsonColumns_RoundTrip()
    {
        var projectId = Guid.NewGuid();
        _context.MigrationProjects.Add(new MigrationProject { Id = projectId, Name = "Assessment Test", CreatedBy = "user" });
        await _context.SaveChangesAsync();

        var report = new AssessmentReport
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            CompatibilityScore = 95.5,
            Risk = RiskRating.Low,
            Schema = new SchemaInventory
            {
                TableCount = 10,
                ViewCount = 3,
                StoredProcedureCount = 5,
                IndexCount = 20,
                TriggerCount = 2,
                Objects =
                [
                    new SchemaObject { Name = "Users", Schema = "dbo", ObjectType = "Table" },
                    new SchemaObject { Name = "Orders", Schema = "sales", ObjectType = "Table" }
                ]
            },
            DataProfile = new DataProfile
            {
                TotalRowCount = 1_000_000,
                TotalSizeBytes = 5_000_000,
                Tables =
                [
                    new TableProfile { SchemaName = "dbo", TableName = "Users", RowCount = 500_000, SizeBytes = 2_500_000 }
                ]
            },
            Performance = new PerformanceProfile
            {
                AvgCpuPercent = 45.5,
                MemoryUsedMb = 2048,
                AvgIoMbPerSecond = 100.5,
                MaxDatabaseSizeMb = 10_000
            },
            CompatibilityIssues =
            [
                new CompatibilityIssue
                {
                    ObjectName = "dbo.LegacyProc",
                    IssueType = "Unsupported Feature",
                    Description = "Uses deprecated syntax",
                    IsBlocking = false
                }
            ],
            Recommendation = new TierRecommendation
            {
                ServiceTier = "GeneralPurpose",
                ComputeSize = "GP_Gen5_2",
                VCores = 2,
                StorageGb = 32,
                EstimatedMonthlyCostUsd = 150.00m,
                Reasoning = "Standard workload"
            }
        };

        _context.AssessmentReports.Add(report);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var retrieved = await _context.AssessmentReports.FirstAsync(a => a.Id == report.Id);

        // Schema
        Assert.Equal(10, retrieved.Schema.TableCount);
        Assert.Equal(3, retrieved.Schema.ViewCount);
        Assert.Equal(5, retrieved.Schema.StoredProcedureCount);
        Assert.Equal(20, retrieved.Schema.IndexCount);
        Assert.Equal(2, retrieved.Schema.TriggerCount);
        Assert.Equal(2, retrieved.Schema.Objects.Count);
        Assert.Equal("Users", retrieved.Schema.Objects[0].Name);
        Assert.Equal("sales", retrieved.Schema.Objects[1].Schema);

        // DataProfile
        Assert.Equal(1_000_000, retrieved.DataProfile.TotalRowCount);
        Assert.Equal(5_000_000, retrieved.DataProfile.TotalSizeBytes);
        Assert.Single(retrieved.DataProfile.Tables);
        Assert.Equal("Users", retrieved.DataProfile.Tables[0].TableName);
        Assert.Equal(500_000, retrieved.DataProfile.Tables[0].RowCount);

        // Performance
        Assert.Equal(45.5, retrieved.Performance.AvgCpuPercent);
        Assert.Equal(2048, retrieved.Performance.MemoryUsedMb);
        Assert.Equal(100.5, retrieved.Performance.AvgIoMbPerSecond);
        Assert.Equal(10_000, retrieved.Performance.MaxDatabaseSizeMb);

        // CompatibilityIssues
        Assert.Single(retrieved.CompatibilityIssues);
        Assert.Equal("dbo.LegacyProc", retrieved.CompatibilityIssues[0].ObjectName);
        Assert.Equal("Unsupported Feature", retrieved.CompatibilityIssues[0].IssueType);
        Assert.False(retrieved.CompatibilityIssues[0].IsBlocking);
    }

    [Fact]
    public async Task EnumProperties_RoundTripCorrectly()
    {
        var project = new MigrationProject
        {
            Id = Guid.NewGuid(),
            Name = "Enum Test",
            CreatedBy = "user",
            Status = ProjectStatus.Migrating
        };
        _context.MigrationProjects.Add(project);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var retrieved = await _context.MigrationProjects.FindAsync(project.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(ProjectStatus.Migrating, retrieved!.Status);
    }

    [Fact]
    public async Task MigrationResult_WithValidationResults_SavesCorrectly()
    {
        var projectId = Guid.NewGuid();
        _context.MigrationProjects.Add(new MigrationProject { Id = projectId, Name = "Result Test", CreatedBy = "user" });
        await _context.SaveChangesAsync();

        var result = new MigrationResult
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Success = true,
            StartedAt = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2024, 1, 1, 11, 0, 0, DateTimeKind.Utc),
            RowsMigrated = 500_000,
            DataSizeBytes = 2_500_000,
            Validations =
            [
                new ValidationResult { TableName = "Users", SourceRowCount = 1000, TargetRowCount = 1000, ChecksumMatch = true },
                new ValidationResult { TableName = "Orders", SourceRowCount = 5000, TargetRowCount = 5000, ChecksumMatch = true }
            ],
            Errors = ["Warning: Index rebuild recommended"]
        };

        _context.MigrationResults.Add(result);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var retrieved = await _context.MigrationResults.FirstAsync(r => r.Id == result.Id);

        Assert.True(retrieved.Success);
        Assert.Equal(500_000, retrieved.RowsMigrated);
        Assert.Equal(2_500_000, retrieved.DataSizeBytes);
        Assert.Equal(2, retrieved.Validations.Count);
        Assert.Equal("Users", retrieved.Validations[0].TableName);
        Assert.Equal(1000, retrieved.Validations[0].SourceRowCount);
        Assert.True(retrieved.Validations[0].ChecksumMatch);
        Assert.Single(retrieved.Errors);
        Assert.Equal("Warning: Index rebuild recommended", retrieved.Errors[0]);
    }
}
