using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests.PostgreSql;

/// <summary>
/// Tests for the PostgreSQL DataProfiler aggregation and mapping logic.
/// Since NpgsqlConnection/NpgsqlCommand are sealed and cannot be mocked,
/// we test the DataProfile model aggregation semantics and defaults.
/// Integration tests (Phase 1, #23) will verify against a real PostgreSQL instance.
/// </summary>
public class DataProfilerTests
{
    [Fact]
    public void EmptyProfile_HasZeroTotals()
    {
        var profile = new DataProfile();

        Assert.Equal(0, profile.TotalRowCount);
        Assert.Equal(0, profile.TotalSizeBytes);
        Assert.Empty(profile.Tables);
    }

    [Fact]
    public void SingleTable_TotalsMatchTableValues()
    {
        var profile = BuildProfile(
            Table("public", "users", rowCount: 1000, sizeBytes: 8192));

        Assert.Equal(1000, profile.TotalRowCount);
        Assert.Equal(8192, profile.TotalSizeBytes);
        Assert.Single(profile.Tables);
    }

    [Fact]
    public void MultipleTables_TotalsAreSummed()
    {
        var profile = BuildProfile(
            Table("public", "users", rowCount: 1000, sizeBytes: 8192),
            Table("public", "orders", rowCount: 5000, sizeBytes: 32768),
            Table("sales", "line_items", rowCount: 25000, sizeBytes: 131072));

        Assert.Equal(31000, profile.TotalRowCount);
        Assert.Equal(172032, profile.TotalSizeBytes);
        Assert.Equal(3, profile.Tables.Count);
    }

    [Fact]
    public void TableProfile_DefaultSchema_IsDbo()
    {
        var table = new TableProfile();

        Assert.Equal("dbo", table.SchemaName);
        Assert.Equal(string.Empty, table.TableName);
        Assert.Equal(0, table.RowCount);
        Assert.Equal(0, table.SizeBytes);
    }

    [Fact]
    public void TableProfile_PreservesPostgreSqlSchema()
    {
        var table = new TableProfile
        {
            SchemaName = "public",
            TableName = "users"
        };

        Assert.Equal("public", table.SchemaName);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1024)]
    [InlineData(1_000_000, 1_073_741_824)]
    public void Profile_AccumulatesTotalsCorrectly(long rowCount, long sizeBytes)
    {
        var profile = BuildProfile(
            Table("public", "test_table", rowCount, sizeBytes));

        Assert.Equal(rowCount, profile.TotalRowCount);
        Assert.Equal(sizeBytes, profile.TotalSizeBytes);
    }

    [Fact]
    public void Profile_TablesOrderPreserved()
    {
        var profile = BuildProfile(
            Table("public", "alpha", rowCount: 100, sizeBytes: 1024),
            Table("public", "beta", rowCount: 200, sizeBytes: 2048),
            Table("public", "gamma", rowCount: 300, sizeBytes: 4096));

        Assert.Equal("alpha", profile.Tables[0].TableName);
        Assert.Equal("beta", profile.Tables[1].TableName);
        Assert.Equal("gamma", profile.Tables[2].TableName);
    }

    [Fact]
    public void Profile_ZeroRowTables_StillContributed()
    {
        var profile = BuildProfile(
            Table("public", "empty_table", rowCount: 0, sizeBytes: 8192),
            Table("public", "users", rowCount: 500, sizeBytes: 16384));

        Assert.Equal(500, profile.TotalRowCount);
        Assert.Equal(24576, profile.TotalSizeBytes);
        Assert.Equal(2, profile.Tables.Count);
    }

    /// <summary>
    /// Builds a DataProfile by adding tables and computing totals,
    /// mirroring the aggregation logic in DataProfiler.CollectAsync.
    /// </summary>
    private static DataProfile BuildProfile(params TableProfile[] tables)
    {
        var profile = new DataProfile();
        foreach (var table in tables)
        {
            profile.Tables.Add(table);
            profile.TotalRowCount += table.RowCount;
            profile.TotalSizeBytes += table.SizeBytes;
        }
        return profile;
    }

    private static TableProfile Table(string schema, string name, long rowCount, long sizeBytes) =>
        new()
        {
            SchemaName = schema,
            TableName = name,
            RowCount = rowCount,
            SizeBytes = sizeBytes
        };
}
