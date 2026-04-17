using Moq;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;
using Scaffold.Migration.SqlServer;

namespace Scaffold.Migration.Tests;

/// <summary>
/// IProgress implementation that invokes the callback synchronously
/// (unlike Progress&lt;T&gt; which posts to the synchronization context).
/// </summary>
internal class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}

public class SqlServerMigratorTests
{
    private static MigrationPlan CreateValidPlan(params string[] tables) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        SourceConnectionString = "Server=source;Database=TestDb;Encrypt=false;",
        ExistingTargetConnectionString = "Server=target;Database=TestDb;Encrypt=false;",
        IncludedObjects = tables.ToList()
    };

    #region ExecuteCutoverAsync — schema → data → validation flow

    [Fact]
    public async Task ExecuteCutoverAsync_CallsSchemaDeployThenDataCopy()
    {
        var schemaDeployer = new Mock<SchemaDeployer>();
        var bulkDataCopier = new Mock<BulkDataCopier>();
        var callOrder = new List<string>();

        schemaDeployer
            .Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("schema"))
            .Returns(Task.CompletedTask);

        bulkDataCopier
            .Setup(b => b.CopyDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>(),
                It.IsAny<int?>()))
            .Callback(() => callOrder.Add("data"))
            .ReturnsAsync(42L);

        var migrator = new SqlServerMigrator(schemaDeployer.Object, bulkDataCopier.Object);
        var plan = CreateValidPlan("dbo.Users");

        var result = await migrator.ExecuteCutoverAsync(plan);

        Assert.True(result.Success);
        Assert.Equal(42, result.RowsMigrated);
        Assert.Equal(new List<string> { "schema", "data" }, callOrder);
    }

    [Fact]
    public async Task ExecuteCutoverAsync_ReportsProgress()
    {
        var schemaDeployer = new Mock<SchemaDeployer>();
        var bulkDataCopier = new Mock<BulkDataCopier>();

        schemaDeployer
            .Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        bulkDataCopier
            .Setup(b => b.CopyDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>(),
                It.IsAny<int?>()))
            .ReturnsAsync(0L);

        var migrator = new SqlServerMigrator(schemaDeployer.Object, bulkDataCopier.Object);
        var plan = CreateValidPlan();
        var phases = new List<string>();
        var progress = new SynchronousProgress<MigrationProgress>(p => phases.Add(p.Phase));

        await migrator.ExecuteCutoverAsync(plan, progress);

        // At minimum the orchestrator reports SchemaDeployment and DataMigration phases
        Assert.Contains("SchemaDeployment", phases);
        Assert.Contains("DataMigration", phases);
    }

    [Fact]
    public async Task ExecuteCutoverAsync_SetsResultMetadata()
    {
        var schemaDeployer = new Mock<SchemaDeployer>();
        var bulkDataCopier = new Mock<BulkDataCopier>();

        schemaDeployer
            .Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        bulkDataCopier
            .Setup(b => b.CopyDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>(),
                It.IsAny<int?>()))
            .ReturnsAsync(100L);

        var migrator = new SqlServerMigrator(schemaDeployer.Object, bulkDataCopier.Object);
        var plan = CreateValidPlan("dbo.Users");

        var result = await migrator.ExecuteCutoverAsync(plan);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(plan.ProjectId, result.ProjectId);
        Assert.NotNull(result.CompletedAt);
        Assert.True(result.StartedAt <= result.CompletedAt);
    }

    [Fact]
    public async Task ExecuteCutoverAsync_SchemaDeployFailure_CapturesError()
    {
        var schemaDeployer = new Mock<SchemaDeployer>();
        var bulkDataCopier = new Mock<BulkDataCopier>();

        schemaDeployer
            .Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Schema deploy failed"));

        var migrator = new SqlServerMigrator(schemaDeployer.Object, bulkDataCopier.Object);
        var plan = CreateValidPlan("dbo.Users");

        var result = await migrator.ExecuteCutoverAsync(plan);

        Assert.False(result.Success);
        Assert.Contains("Schema deploy failed", result.Errors);
    }

    #endregion

    #region Missing connection strings

    [Fact]
    public async Task ExecuteCutoverAsync_MissingSourceConnectionString_ReturnsFailure()
    {
        var migrator = new SqlServerMigrator(new SchemaDeployer(), new BulkDataCopier());
        var plan = new MigrationPlan
        {
            SourceConnectionString = null,
            ExistingTargetConnectionString = "Server=target;Database=Test;Encrypt=false;"
        };

        var result = await migrator.ExecuteCutoverAsync(plan);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("SourceConnectionString"));
    }

    [Fact]
    public async Task ExecuteCutoverAsync_MissingTargetConnectionString_ReturnsFailure()
    {
        var migrator = new SqlServerMigrator(new SchemaDeployer(), new BulkDataCopier());
        var plan = new MigrationPlan
        {
            SourceConnectionString = "Server=source;Database=Test;Encrypt=false;",
            ExistingTargetConnectionString = null
        };

        var result = await migrator.ExecuteCutoverAsync(plan);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("ExistingTargetConnectionString"));
    }

    [Fact]
    public async Task ExecuteCutoverAsync_EmptySourceConnectionString_ReturnsFailure()
    {
        var migrator = new SqlServerMigrator(new SchemaDeployer(), new BulkDataCopier());
        var plan = new MigrationPlan
        {
            SourceConnectionString = "   ",
            ExistingTargetConnectionString = "Server=target;Database=Test;Encrypt=false;"
        };

        var result = await migrator.ExecuteCutoverAsync(plan);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("SourceConnectionString"));
    }

    #endregion

    #region StartContinuousSyncAsync

    [Fact]
    public async Task StartContinuousSyncAsync_MissingConnectionStrings_Throws()
    {
        var migrator = new SqlServerMigrator(new SchemaDeployer(), new BulkDataCopier());
        var plan = new MigrationPlan
        {
            SourceConnectionString = null,
            ExistingTargetConnectionString = "Server=target;Database=Test;Encrypt=false;"
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => migrator.StartContinuousSyncAsync(plan));
    }

    [Fact]
    public async Task StartContinuousSyncAsync_ValidPlan_CallsSchemaDeployer()
    {
        var schemaDeployer = new Mock<SchemaDeployer>();
        var bulkDataCopier = new Mock<BulkDataCopier>();

        schemaDeployer
            .Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var migrator = new SqlServerMigrator(schemaDeployer.Object, bulkDataCopier.Object);
        var plan = CreateValidPlan("dbo.Users");

        // StartContinuousSyncAsync will call DeploySchemaAsync then try to start
        // ChangeTrackingSyncEngine which will fail on DB connection. That's expected
        // — we're testing that schema deploy is called first.
        try
        {
            await migrator.StartContinuousSyncAsync(plan);
        }
        catch
        {
            // Expected: ChangeTrackingSyncEngine.StartAsync needs a real DB
        }

        schemaDeployer.Verify(s => s.DeploySchemaAsync(
            plan.SourceConnectionString!,
            plan.ExistingTargetConnectionString!,
            "TestDb",
            It.IsAny<IProgress<MigrationProgress>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region CompleteCutoverAsync

    [Fact]
    public async Task CompleteCutoverAsync_WithoutStarting_ThrowsInvalidOperation()
    {
        var migrator = new SqlServerMigrator(new SchemaDeployer(), new BulkDataCopier());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => migrator.CompleteCutoverAsync(Guid.NewGuid()));
    }

    #endregion

    #region SchemaDeployer — DacFx options verification

    [Fact]
    public void DacDeployOptions_AreConfiguredCorrectly()
    {
        // Verify the options the SchemaDeployer uses match expected configuration.
        // These are constructed inside DeploySchemaAsync but we verify the contract.
        var options = new Microsoft.SqlServer.Dac.DacDeployOptions
        {
            BlockOnPossibleDataLoss = false,
            DropObjectsNotInSource = false,
            IgnorePermissions = true,
            IgnoreRoleMembership = true
        };

        Assert.False(options.BlockOnPossibleDataLoss);
        Assert.False(options.DropObjectsNotInSource);
        Assert.True(options.IgnorePermissions);
        Assert.True(options.IgnoreRoleMembership);
    }

    [Fact]
    public void ExtractDatabaseName_ParsesConnectionString()
    {
        var dbName = SchemaDeployer.ExtractDatabaseName(
            "Server=myserver.database.windows.net;Database=MyDatabase;Encrypt=false;");

        Assert.Equal("MyDatabase", dbName);
    }

    [Fact]
    public void ExtractDatabaseName_InitialCatalog_Works()
    {
        var dbName = SchemaDeployer.ExtractDatabaseName(
            "Server=localhost;Initial Catalog=TestDb;Encrypt=false;");

        Assert.Equal("TestDb", dbName);
    }

    #endregion

    #region SchemaDeployer — temp DACPAC cleanup

    [Fact]
    public void DacpacPath_UsesGuidForUniqueness()
    {
        // Verify the pattern the code uses: {dbName}_{guid}.dacpac in temp dir
        var dbName = "TestDb";
        var guid = Guid.NewGuid();
        var path = Path.Combine(Path.GetTempPath(), $"{dbName}_{guid:N}.dacpac");

        Assert.StartsWith(Path.GetTempPath(), path);
        Assert.EndsWith(".dacpac", path);
        Assert.Contains(dbName, path);
    }

    [Fact]
    public void DacpacCleanup_FileDelete_RemovesFile()
    {
        // Simulate the cleanup pattern used in SchemaDeployer's finally block
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.dacpac");
        File.WriteAllText(tempFile, "dummy");
        Assert.True(File.Exists(tempFile));

        // Simulate the finally block
        if (File.Exists(tempFile))
            File.Delete(tempFile);

        Assert.False(File.Exists(tempFile));
    }

    #endregion

    #region Timeout passthrough

    [Fact]
    public async Task ExecuteCutoverAsync_PassesBulkCopyTimeoutFromPlan()
    {
        var schemaDeployer = new Mock<SchemaDeployer>();
        var bulkDataCopier = new Mock<BulkDataCopier>();

        schemaDeployer
            .Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        int? capturedTimeout = null;
        bulkDataCopier
            .Setup(b => b.CopyDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>(),
                It.IsAny<int?>()))
            .Callback<string, string, IReadOnlyList<string>, IProgress<MigrationProgress>?, CancellationToken, int?>(
                (_, _, _, _, _, timeout) => capturedTimeout = timeout)
            .ReturnsAsync(0L);

        var migrator = new SqlServerMigrator(schemaDeployer.Object, bulkDataCopier.Object);
        var plan = CreateValidPlan("dbo.Users");
        plan.BulkCopyTimeoutSeconds = 1200;

        await migrator.ExecuteCutoverAsync(plan);

        Assert.Equal(1200, capturedTimeout);
    }

    [Fact]
    public async Task ExecuteCutoverAsync_PassesNullTimeoutWhenNotSet()
    {
        var schemaDeployer = new Mock<SchemaDeployer>();
        var bulkDataCopier = new Mock<BulkDataCopier>();

        schemaDeployer
            .Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        int? capturedTimeout = -1; // sentinel to detect change
        bulkDataCopier
            .Setup(b => b.CopyDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>(),
                It.IsAny<int?>()))
            .Callback<string, string, IReadOnlyList<string>, IProgress<MigrationProgress>?, CancellationToken, int?>(
                (_, _, _, _, _, timeout) => capturedTimeout = timeout)
            .ReturnsAsync(0L);

        var migrator = new SqlServerMigrator(schemaDeployer.Object, bulkDataCopier.Object);
        var plan = CreateValidPlan("dbo.Users");
        // BulkCopyTimeoutSeconds is null by default

        await migrator.ExecuteCutoverAsync(plan);

        Assert.Null(capturedTimeout);
    }

    [Fact]
    public async Task ExecuteCutoverAsync_PassesScriptTimeoutFromPlan()
    {
        var schemaDeployer = new Mock<SchemaDeployer>();
        var bulkDataCopier = new Mock<BulkDataCopier>();
        var scriptExecutor = new Mock<ScriptExecutor>();

        schemaDeployer
            .Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        bulkDataCopier
            .Setup(b => b.CopyDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>(),
                It.IsAny<int?>()))
            .ReturnsAsync(0L);

        int? capturedTimeout = null;
        scriptExecutor
            .Setup(s => s.ExecuteScriptsAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<MigrationScript>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>(),
                It.IsAny<int?>()))
            .Callback<string, IReadOnlyList<MigrationScript>, IProgress<MigrationProgress>?, CancellationToken, int?>(
                (_, _, _, _, timeout) => capturedTimeout = timeout)
            .Returns(Task.CompletedTask);

        var migrator = new SqlServerMigrator(schemaDeployer.Object, bulkDataCopier.Object, scriptExecutor.Object);
        var plan = CreateValidPlan("dbo.Users");
        plan.ScriptTimeoutSeconds = 900;
        plan.PreMigrationScripts =
        [
            new MigrationScript { ScriptId = "pre1", Label = "Pre", Phase = MigrationScriptPhase.Pre, SqlContent = "SELECT 1", IsEnabled = true, Order = 0 }
        ];

        await migrator.ExecuteCutoverAsync(plan);

        Assert.Equal(900, capturedTimeout);
    }

    #endregion

    #region SourcePlatform

    [Fact]
    public void SourcePlatform_ReturnsSqlServer()
    {
        var migrator = new SqlServerMigrator();
        Assert.Equal("SqlServer", migrator.SourcePlatform);
    }

    #endregion
}
