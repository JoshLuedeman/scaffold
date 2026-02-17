using Scaffold.Assessment.SqlServer;

namespace Scaffold.Integration.Tests;

/// <summary>
/// Integration tests for DataProfiler against a real SQL Server instance.
/// </summary>
[Collection("SqlServer")]
public class DataProfilerIntegrationTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public DataProfilerIntegrationTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CollectAsync_ReturnsTableProfiles()
    {
        var profile = await DataProfiler.CollectAsync(_fixture.Connection);

        Assert.NotNull(profile);
        Assert.True(profile.Tables.Count >= 5, $"Expected at least 5 tables, got {profile.Tables.Count}");
    }

    [Fact]
    public async Task CollectAsync_TotalRowCountIsPositive()
    {
        var profile = await DataProfiler.CollectAsync(_fixture.Connection);

        Assert.True(profile.TotalRowCount > 0, $"Expected positive row count, got {profile.TotalRowCount}");
    }

    [Fact]
    public async Task CollectAsync_TotalSizeBytesIsPositive()
    {
        var profile = await DataProfiler.CollectAsync(_fixture.Connection);

        Assert.True(profile.TotalSizeBytes > 0, $"Expected positive size, got {profile.TotalSizeBytes}");
    }

    [Fact]
    public async Task CollectAsync_UsersTableHasSeededRows()
    {
        var profile = await DataProfiler.CollectAsync(_fixture.Connection);

        var usersTable = profile.Tables.FirstOrDefault(t => t.TableName == "Users");
        Assert.NotNull(usersTable);
        Assert.True(usersTable.RowCount >= 3, $"Expected at least 3 Users rows, got {usersTable.RowCount}");
    }

    [Fact]
    public async Task CollectAsync_TableProfilesHaveSchemaName()
    {
        var profile = await DataProfiler.CollectAsync(_fixture.Connection);

        Assert.All(profile.Tables, t =>
        {
            Assert.False(string.IsNullOrEmpty(t.SchemaName), $"Table {t.TableName} has empty schema");
        });
    }
}
