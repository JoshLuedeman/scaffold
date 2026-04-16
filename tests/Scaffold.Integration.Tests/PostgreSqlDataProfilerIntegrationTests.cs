using Scaffold.Assessment.PostgreSql;

namespace Scaffold.Integration.Tests;

/// <summary>
/// Integration tests for PostgreSQL DataProfiler against a real PostgreSQL instance.
/// </summary>
[Collection("PostgreSql")]
public class PostgreSqlDataProfilerIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    public PostgreSqlDataProfilerIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CollectAsync_ReturnsTotalSize()
    {
        var profile = await DataProfiler.CollectAsync(_fixture.Connection);

        Assert.True(profile.TotalSizeBytes > 0, "Expected total size > 0");
    }

    [Fact]
    public async Task CollectAsync_ReturnsRowCount()
    {
        var profile = await DataProfiler.CollectAsync(_fixture.Connection);

        // May be 0 if statistics haven't been updated, but should not be negative
        Assert.True(profile.TotalRowCount >= 0);
    }

    [Fact]
    public async Task CollectAsync_ReturnsTableProfiles()
    {
        var profile = await DataProfiler.CollectAsync(_fixture.Connection);

        Assert.NotEmpty(profile.Tables);
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