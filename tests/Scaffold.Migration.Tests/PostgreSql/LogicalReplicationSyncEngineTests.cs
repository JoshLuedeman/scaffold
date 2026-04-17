using Moq;
using Scaffold.Core.Interfaces;
using Scaffold.Migration.PostgreSql;

namespace Scaffold.Migration.Tests.PostgreSql;

public class LogicalReplicationSyncEngineTests
{
    private static PostgreSqlBulkCopier CreateMockBulkCopier()
        => new Mock<PostgreSqlBulkCopier>().Object;

    #region Constructor

    [Fact]
    public void Constructor_NullBulkCopier_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new LogicalReplicationSyncEngine(null!));
        Assert.Equal("bulkCopier", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidBulkCopier_Succeeds()
    {
        var engine = new LogicalReplicationSyncEngine(CreateMockBulkCopier());

        Assert.NotNull(engine);
    }

    [Fact]
    public void Constructor_WithProgress_Succeeds()
    {
        var progress = new Progress<MigrationProgress>();
        var engine = new LogicalReplicationSyncEngine(CreateMockBulkCopier(), progress);

        Assert.NotNull(engine);
    }

    [Fact]
    public void Constructor_WithCustomPollInterval_Succeeds()
    {
        var engine = new LogicalReplicationSyncEngine(
            CreateMockBulkCopier(),
            pollInterval: TimeSpan.FromSeconds(10));

        Assert.NotNull(engine);
    }

    #endregion

    #region Initial state

    [Fact]
    public void TotalRowsSynced_Initially_IsZero()
    {
        var engine = new LogicalReplicationSyncEngine(CreateMockBulkCopier());

        Assert.Equal(0, engine.TotalRowsSynced);
    }

    [Fact]
    public void IsRunning_Initially_IsFalse()
    {
        var engine = new LogicalReplicationSyncEngine(CreateMockBulkCopier());

        Assert.False(engine.IsRunning);
    }

    #endregion

    #region GenerateSlotName

    [Fact]
    public void GenerateSlotName_ContainsPrefix()
    {
        var id = Guid.NewGuid();
        var slotName = LogicalReplicationSyncEngine.GenerateSlotName(id);

        Assert.StartsWith("scaffold_", slotName);
    }

    [Fact]
    public void GenerateSlotName_ContainsMigrationId()
    {
        var id = Guid.NewGuid();
        var slotName = LogicalReplicationSyncEngine.GenerateSlotName(id);

        Assert.Contains(id.ToString("N"), slotName);
    }

    [Fact]
    public void GenerateSlotName_MaxLength63()
    {
        var id = Guid.NewGuid();
        var slotName = LogicalReplicationSyncEngine.GenerateSlotName(id);

        Assert.True(slotName.Length <= 63,
            $"Slot name '{slotName}' exceeds 63 chars (length: {slotName.Length})");
    }

    [Fact]
    public void GenerateSlotName_IsLowercase()
    {
        var id = Guid.NewGuid();
        var slotName = LogicalReplicationSyncEngine.GenerateSlotName(id);

        Assert.Equal(slotName, slotName.ToLowerInvariant());
    }

    [Fact]
    public void GenerateSlotName_ContainsOnlyValidChars()
    {
        var id = Guid.NewGuid();
        var slotName = LogicalReplicationSyncEngine.GenerateSlotName(id);

        Assert.Matches("^[a-z0-9_]+$", slotName);
    }

    [Fact]
    public void GenerateSlotName_DifferentIds_ProduceDifferentNames()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var name1 = LogicalReplicationSyncEngine.GenerateSlotName(id1);
        var name2 = LogicalReplicationSyncEngine.GenerateSlotName(id2);

        Assert.NotEqual(name1, name2);
    }

    [Fact]
    public void GenerateSlotName_SameId_ProducesSameName()
    {
        var id = Guid.NewGuid();

        var name1 = LogicalReplicationSyncEngine.GenerateSlotName(id);
        var name2 = LogicalReplicationSyncEngine.GenerateSlotName(id);

        Assert.Equal(name1, name2);
    }

    [Fact]
    public void GenerateSlotName_EmptyGuid_ProducesValidName()
    {
        var slotName = LogicalReplicationSyncEngine.GenerateSlotName(Guid.Empty);

        Assert.StartsWith("scaffold_", slotName);
        Assert.Matches("^[a-z0-9_]+$", slotName);
    }

    #endregion

    #region GeneratePublicationName

    [Fact]
    public void GeneratePublicationName_ContainsPrefix()
    {
        var id = Guid.NewGuid();
        var pubName = LogicalReplicationSyncEngine.GeneratePublicationName(id);

        Assert.StartsWith("scaffold_pub_", pubName);
    }

    [Fact]
    public void GeneratePublicationName_MaxLength63()
    {
        var id = Guid.NewGuid();
        var pubName = LogicalReplicationSyncEngine.GeneratePublicationName(id);

        Assert.True(pubName.Length <= 63,
            $"Publication name '{pubName}' exceeds 63 chars (length: {pubName.Length})");
    }

    [Fact]
    public void GeneratePublicationName_DifferentIds_ProduceDifferentNames()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var name1 = LogicalReplicationSyncEngine.GeneratePublicationName(id1);
        var name2 = LogicalReplicationSyncEngine.GeneratePublicationName(id2);

        Assert.NotEqual(name1, name2);
    }

    [Fact]
    public void GeneratePublicationName_SameId_ProducesSameName()
    {
        var id = Guid.NewGuid();

        var name1 = LogicalReplicationSyncEngine.GeneratePublicationName(id);
        var name2 = LogicalReplicationSyncEngine.GeneratePublicationName(id);

        Assert.Equal(name1, name2);
    }

    #endregion

    #region StartAsync validation

    [Fact]
    public async Task StartAsync_NullSourceConnection_ThrowsArgumentException()
    {
        var engine = new LogicalReplicationSyncEngine(CreateMockBulkCopier());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => engine.StartAsync(null!, "target", ["table1"], Guid.NewGuid()));
        Assert.Equal("sourceConnectionString", ex.ParamName);
    }

    [Fact]
    public async Task StartAsync_EmptySourceConnection_ThrowsArgumentException()
    {
        var engine = new LogicalReplicationSyncEngine(CreateMockBulkCopier());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => engine.StartAsync("", "target", ["table1"], Guid.NewGuid()));
        Assert.Equal("sourceConnectionString", ex.ParamName);
    }

    [Fact]
    public async Task StartAsync_WhitespaceSourceConnection_ThrowsArgumentException()
    {
        var engine = new LogicalReplicationSyncEngine(CreateMockBulkCopier());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => engine.StartAsync("   ", "target", ["table1"], Guid.NewGuid()));
        Assert.Equal("sourceConnectionString", ex.ParamName);
    }

    [Fact]
    public async Task StartAsync_NullTargetConnection_ThrowsArgumentException()
    {
        var engine = new LogicalReplicationSyncEngine(CreateMockBulkCopier());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => engine.StartAsync("source", null!, ["table1"], Guid.NewGuid()));
        Assert.Equal("targetConnectionString", ex.ParamName);
    }

    [Fact]
    public async Task StartAsync_EmptyTargetConnection_ThrowsArgumentException()
    {
        var engine = new LogicalReplicationSyncEngine(CreateMockBulkCopier());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => engine.StartAsync("source", "", ["table1"], Guid.NewGuid()));
        Assert.Equal("targetConnectionString", ex.ParamName);
    }

    #endregion

    #region CompleteCutoverAsync validation

    [Fact]
    public async Task CompleteCutoverAsync_BeforeStart_ThrowsInvalidOperationException()
    {
        var engine = new LogicalReplicationSyncEngine(CreateMockBulkCopier());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.CompleteCutoverAsync(Guid.NewGuid()));
        Assert.Contains("StartAsync", ex.Message);
    }

    #endregion

    #region Cancellation

    [Fact]
    public async Task StartAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var engine = new LogicalReplicationSyncEngine(CreateMockBulkCopier());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Should throw since the engine tries to validate wal_level with a cancelled token
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => engine.StartAsync(
                "Host=fake;Database=test;",
                "Host=fake;Database=test;",
                ["public.test"],
                Guid.NewGuid(),
                cts.Token));
    }

    #endregion

    #region Name format consistency

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("12345678-1234-1234-1234-123456789abc")]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff")]
    public void GenerateSlotName_VariousGuids_AllValid(string guidStr)
    {
        var id = Guid.Parse(guidStr);
        var slotName = LogicalReplicationSyncEngine.GenerateSlotName(id);

        Assert.StartsWith("scaffold_", slotName);
        Assert.True(slotName.Length <= 63);
        Assert.Matches("^[a-z0-9_]+$", slotName);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("12345678-1234-1234-1234-123456789abc")]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff")]
    public void GeneratePublicationName_VariousGuids_AllValid(string guidStr)
    {
        var id = Guid.Parse(guidStr);
        var pubName = LogicalReplicationSyncEngine.GeneratePublicationName(id);

        Assert.StartsWith("scaffold_pub_", pubName);
        Assert.True(pubName.Length <= 63);
    }

    #endregion
}
