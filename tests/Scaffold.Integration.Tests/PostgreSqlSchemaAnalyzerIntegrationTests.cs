using Scaffold.Assessment.PostgreSql;

namespace Scaffold.Integration.Tests;

/// <summary>
/// Integration tests for PostgreSQL SchemaAnalyzer against a real PostgreSQL instance.
/// These tests require a running PostgreSQL with the sample database seeded.
/// </summary>
[Collection("PostgreSql")]
public class PostgreSqlSchemaAnalyzerIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    public PostgreSqlSchemaAnalyzerIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsTables()
    {
        var analyzer = new SchemaAnalyzer(_fixture.Connection);

        var inventory = await analyzer.AnalyzeAsync();

        Assert.True(inventory.TableCount >= 3, $"Expected at least 3 tables, got {inventory.TableCount}");
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsViews()
    {
        var analyzer = new SchemaAnalyzer(_fixture.Connection);

        var inventory = await analyzer.AnalyzeAsync();

        Assert.True(inventory.ViewCount >= 1, $"Expected at least 1 view, got {inventory.ViewCount}");
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsFunctions()
    {
        var analyzer = new SchemaAnalyzer(_fixture.Connection);

        var inventory = await analyzer.AnalyzeAsync();

        Assert.Contains(inventory.Objects, o => o.ObjectType == "Function");
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsIndexes()
    {
        var analyzer = new SchemaAnalyzer(_fixture.Connection);

        var inventory = await analyzer.AnalyzeAsync();

        Assert.True(inventory.IndexCount >= 1, $"Expected at least 1 index, got {inventory.IndexCount}");
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsTriggers()
    {
        var analyzer = new SchemaAnalyzer(_fixture.Connection);

        var inventory = await analyzer.AnalyzeAsync();

        Assert.True(inventory.TriggerCount >= 1, $"Expected at least 1 trigger, got {inventory.TriggerCount}");
    }

    [Fact]
    public async Task AnalyzeAsync_ObjectsHaveSchemaSet()
    {
        var analyzer = new SchemaAnalyzer(_fixture.Connection);

        var inventory = await analyzer.AnalyzeAsync();

        Assert.All(inventory.Objects, obj =>
        {
            Assert.False(string.IsNullOrEmpty(obj.Schema), $"Object {obj.Name} has empty schema");
        });
    }
}