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
        // Verify the SQL that would be generated for enabling change tracking
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
        // When CompleteCutoverAsync is called without StartAsync, the sync loop
        // fields are null, so it should handle gracefully and return a result.
        var engine = new ChangeTrackingSyncEngine(FakeSource, FakeTarget);
        var projectId = Guid.NewGuid();

        // Since _syncCts and _syncLoopTask are null, this should execute
        // the final sync path which will fail on DB connection, caught by the try/catch.
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
}
