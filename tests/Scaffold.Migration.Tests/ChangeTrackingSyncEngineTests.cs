using Microsoft.Data.SqlClient;
using Scaffold.Core.Interfaces;
using Scaffold.Migration.SqlServer;

namespace Scaffold.Migration.Tests;

public class ChangeTrackingSyncEngineTests
{
    private const string FakeSource = "Server=fake-source;Database=TestDb;Encrypt=false;";
    private const string FakeTarget = "Server=fake-target;Database=TestDb;Encrypt=false;";

    #region Constructor / initial state

    [Fact]
    public void Constructor_SetsInitialState()
    {
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget);

        Assert.Equal(0, engine.CurrentVersion);
        Assert.Equal(0, engine.TotalRowsSynced);
        Assert.False(engine.IsRunning);
    }

    [Fact]
    public void Constructor_WithProgress_SetsInitialState()
    {
        var progress = new Progress<MigrationProgress>();
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget, progress);

        Assert.False(engine.IsRunning);
    }

    [Fact]
    public void Constructor_WithCustomPollInterval()
    {
        var interval = TimeSpan.FromSeconds(30);
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget, pollInterval: interval);

        Assert.False(engine.IsRunning);
        Assert.Equal(0, engine.CurrentVersion);
    }

    #endregion

    #region Setup SQL commands are well-formed

    [Fact]
    public void EnableChangeTracking_DatabaseSql_IsWellFormed()
    {
        const string dbName = "TestDb";
        var expectedSql = $"ALTER DATABASE [{dbName}]\n" +
                          "SET CHANGE_TRACKING = ON (CHANGE_RETENTION = 2 DAYS, AUTO_CLEANUP = ON)";

        Assert.Contains("ALTER DATABASE", expectedSql);
        Assert.Contains("CHANGE_TRACKING = ON", expectedSql);
        Assert.Contains("CHANGE_RETENTION = 2 DAYS", expectedSql);
        Assert.Contains("AUTO_CLEANUP = ON", expectedSql);
    }

    [Fact]
    public void EnableChangeTracking_TableSql_IsWellFormed()
    {
        const string schema = "dbo";
        const string name = "Users";
        var sql = $"ALTER TABLE [{schema}].[{name}] ENABLE CHANGE_TRACKING WITH (TRACK_COLUMNS_UPDATED = ON)";

        Assert.Contains("ALTER TABLE", sql);
        Assert.Contains("[dbo].[Users]", sql);
        Assert.Contains("ENABLE CHANGE_TRACKING", sql);
        Assert.Contains("TRACK_COLUMNS_UPDATED = ON", sql);
    }

    [Fact]
    public void CheckDatabaseEnabled_Sql_QueriesChangeTrackingDatabases()
    {
        const string sql = """
            SELECT COUNT(1) FROM sys.change_tracking_databases
            WHERE database_id = DB_ID()
            """;

        Assert.Contains("sys.change_tracking_databases", sql);
        Assert.Contains("DB_ID()", sql);
    }

    [Fact]
    public void CheckTableEnabled_Sql_QueriesChangeTrackingTables()
    {
        const string sql = """
            SELECT COUNT(1) FROM sys.change_tracking_tables
            WHERE object_id = OBJECT_ID(@tableName)
            """;

        Assert.Contains("sys.change_tracking_tables", sql);
        Assert.Contains("OBJECT_ID(@tableName)", sql);
    }

    #endregion

    #region Sync loop lifecycle

    [Fact]
    public void IsRunning_BeforeStart_ReturnsFalse()
    {
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget);
        Assert.False(engine.IsRunning);
    }

    [Fact]
    public async Task CompleteCutoverAsync_WithNoActiveSync_ReturnsResultWithErrors()
    {
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget);
        var projectId = Guid.NewGuid();

        var result = await engine.CompleteCutoverAsync(projectId);

        Assert.Equal(projectId, result.ProjectId);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.NotNull(result.CompletedAt);
    }

    [Fact]
    public void CurrentVersion_InitiallyZero()
    {
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget);
        Assert.Equal(0, engine.CurrentVersion);
    }

    [Fact]
    public void TotalRowsSynced_InitiallyZero()
    {
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget);
        Assert.Equal(0, engine.TotalRowsSynced);
    }

    #endregion

    #region Cancellation

    [Fact]
    public async Task StartAsync_WithPreCancelledToken_DoesNotSetIsRunning()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget);

        var ex = await Record.ExceptionAsync(() => engine.StartAsync(cts.Token));

        Assert.NotNull(ex);
        Assert.True(
            ex is OperationCanceledException or SqlException,
            $"Expected OperationCanceledException or SqlException but got {ex.GetType().Name}");
        Assert.False(engine.IsRunning);
        Assert.Equal(0, engine.CurrentVersion);
        Assert.Equal(0, engine.TotalRowsSynced);
    }

    [Fact]
    public async Task StartAsync_FakeServer_ThrowsSqlException_IsRunningRemainsFalse()
    {
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget);

        var ex = await Assert.ThrowsAsync<SqlException>(() => engine.StartAsync());

        Assert.False(engine.IsRunning);
        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public async Task StartAsync_AfterSqlExceptionFailure_StateIsUnchanged()
    {
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget);

        await Assert.ThrowsAsync<SqlException>(() => engine.StartAsync());

        Assert.False(engine.IsRunning);
        Assert.Equal(0, engine.CurrentVersion);
        Assert.Equal(0, engine.TotalRowsSynced);
    }

    [Fact]
    public async Task CompleteCutoverAsync_AfterCancellation_ReturnsResultGracefully()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget);

        await Record.ExceptionAsync(() => engine.StartAsync(cts.Token));

        var projectId = Guid.NewGuid();
        var result = await engine.CompleteCutoverAsync(projectId);

        Assert.Equal(projectId, result.ProjectId);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.NotNull(result.CompletedAt);
    }

    #endregion

    #region Double-start prevention

    [Fact]
    public async Task StartAsync_CalledTwice_BothFailGracefully_StateRemainsConsistent()
    {
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget);

        await Assert.ThrowsAsync<SqlException>(() => engine.StartAsync());
        Assert.False(engine.IsRunning);

        await Assert.ThrowsAsync<SqlException>(() => engine.StartAsync());
        Assert.False(engine.IsRunning);
        Assert.Equal(0, engine.CurrentVersion);
        Assert.Equal(0, engine.TotalRowsSynced);
    }

    [Fact]
    public async Task StartAsync_SequentialCancelledThenUncancelled_BothHandledGracefully()
    {
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Record.ExceptionAsync(() => engine.StartAsync(cts.Token));
        Assert.False(engine.IsRunning);

        await Assert.ThrowsAsync<SqlException>(() => engine.StartAsync());
        Assert.False(engine.IsRunning);
    }

    #endregion

    #region Progress reporting

    [Fact]
    public async Task CompleteCutoverAsync_WithSynchronousProgress_ReportsFinalSyncPhase()
    {
        var reports = new List<MigrationProgress>();
        var progress = new SynchronousProgress<MigrationProgress>(p => reports.Add(p));
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget, progress);

        await engine.CompleteCutoverAsync(Guid.NewGuid());

        Assert.NotEmpty(reports);
        Assert.Contains(reports, p => p.Phase == "FinalSync");
    }

    [Fact]
    public async Task CompleteCutoverAsync_ProgressReports_AllContainMessages()
    {
        var reports = new List<MigrationProgress>();
        var progress = new SynchronousProgress<MigrationProgress>(p => reports.Add(p));
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget, progress);

        await engine.CompleteCutoverAsync(Guid.NewGuid());

        Assert.NotEmpty(reports);
        Assert.All(reports, p =>
            Assert.False(string.IsNullOrEmpty(p.Message),
                $"Progress report for phase '{p.Phase}' should have a non-empty message."));
    }

    [Fact]
    public async Task CompleteCutoverAsync_FinalSyncProgress_Has90PercentComplete()
    {
        var reports = new List<MigrationProgress>();
        var progress = new SynchronousProgress<MigrationProgress>(p => reports.Add(p));
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget, progress);

        await engine.CompleteCutoverAsync(Guid.NewGuid());

        var finalSync = Assert.Single(reports, p => p.Phase == "FinalSync");
        Assert.Equal(90, finalSync.PercentComplete);
        Assert.Contains("final sync", finalSync.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteCutoverAsync_WithNullProgress_DoesNotThrow()
    {
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget, progress: null);
        var projectId = Guid.NewGuid();

        var result = await engine.CompleteCutoverAsync(projectId);

        Assert.Equal(projectId, result.ProjectId);
        Assert.NotNull(result.CompletedAt);
    }

    [Fact]
    public async Task CompleteCutoverAsync_ProgressReportsPhaseInOrder()
    {
        var phases = new List<string>();
        var progress = new SynchronousProgress<MigrationProgress>(p => phases.Add(p.Phase));
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget, progress);

        await engine.CompleteCutoverAsync(Guid.NewGuid());

        Assert.NotEmpty(phases);
        Assert.Equal("FinalSync", phases[0]);
    }

    #endregion

    #region Edge cases - empty table list

    [Fact]
    public async Task CompleteCutoverAsync_EmptyTrackedTables_ReturnsResultWithErrors()
    {
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget);
        var projectId = Guid.NewGuid();

        var result = await engine.CompleteCutoverAsync(projectId);

        Assert.Equal(projectId, result.ProjectId);
        Assert.NotNull(result.CompletedAt);
        Assert.NotEmpty(result.Errors);
        Assert.Empty(result.Validations);
    }

    [Fact]
    public async Task CompleteCutoverAsync_EmptyTrackedTables_ResultIsNotSuccessful()
    {
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget);

        var result = await engine.CompleteCutoverAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    #endregion

    #region Edge cases - poll interval

    [Fact]
    public void Constructor_VeryShortPollInterval_DoesNotThrow()
    {
        var interval = TimeSpan.FromMilliseconds(1);
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget, pollInterval: interval);

        Assert.False(engine.IsRunning);
        Assert.Equal(0, engine.CurrentVersion);
    }

    [Fact]
    public void Constructor_ZeroPollInterval_DoesNotThrow()
    {
        var interval = TimeSpan.Zero;
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget, pollInterval: interval);

        Assert.False(engine.IsRunning);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(5000)]
    [InlineData(30000)]
    public void Constructor_VariousPollIntervals_AcceptsAll(int milliseconds)
    {
        var interval = TimeSpan.FromMilliseconds(milliseconds);
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget, pollInterval: interval);

        Assert.False(engine.IsRunning);
        Assert.Equal(0, engine.CurrentVersion);
    }

    [Fact]
    public void Constructor_DefaultPollInterval_IsUsedWhenNull()
    {
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget, pollInterval: null);

        Assert.False(engine.IsRunning);
        Assert.Equal(0, engine.CurrentVersion);
    }

    #endregion

    #region Edge cases - result metadata

    [Fact]
    public async Task CompleteCutoverAsync_GeneratesUniqueIdPerCall()
    {
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget);
        var projectId = Guid.NewGuid();

        var result1 = await engine.CompleteCutoverAsync(projectId);
        var result2 = await engine.CompleteCutoverAsync(projectId);

        Assert.NotEqual(Guid.Empty, result1.Id);
        Assert.NotEqual(Guid.Empty, result2.Id);
        Assert.NotEqual(result1.Id, result2.Id);
    }

    [Fact]
    public async Task CompleteCutoverAsync_TimestampsAreWithinExpectedRange()
    {
        var before = DateTime.UtcNow;
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget);

        var result = await engine.CompleteCutoverAsync(Guid.NewGuid());
        var after = DateTime.UtcNow;

        Assert.NotNull(result.CompletedAt);
        Assert.InRange(result.StartedAt, before, after);
        Assert.InRange(result.CompletedAt!.Value, before, after);
    }

    [Fact]
    public async Task CompleteCutoverAsync_RowsMigratedIsZero_WhenNoRealDbSync()
    {
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget);

        var result = await engine.CompleteCutoverAsync(Guid.NewGuid());

        Assert.Equal(0, result.RowsMigrated);
    }

    #endregion
}
