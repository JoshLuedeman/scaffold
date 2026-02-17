using Scaffold.Assessment.SqlServer;
using Scaffold.Core.Models;

namespace Scaffold.Integration.Tests;

/// <summary>
/// Integration tests for SchemaAnalyzer against a real SQL Server instance.
/// These tests require a running SQL Server with the sample database seeded.
/// </summary>
[Collection("SqlServer")]
public class SchemaAnalyzerIntegrationTests
{
    private readonly SqlServerFixture _fixture;

    public SchemaAnalyzerIntegrationTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsTables()
    {
        var analyzer = new SchemaAnalyzer(_fixture.Connection);

        var inventory = await analyzer.AnalyzeAsync();

        Assert.True(inventory.TableCount >= 5, $"Expected at least 5 tables, got {inventory.TableCount}");
        Assert.Contains(inventory.Objects, o => o.ObjectType == "Table" && o.Name == "Users");
        Assert.Contains(inventory.Objects, o => o.ObjectType == "Table" && o.Name == "Orders");
        Assert.Contains(inventory.Objects, o => o.ObjectType == "Table" && o.Name == "Products");
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsViews()
    {
        var analyzer = new SchemaAnalyzer(_fixture.Connection);

        var inventory = await analyzer.AnalyzeAsync();

        Assert.True(inventory.ViewCount >= 2, $"Expected at least 2 views, got {inventory.ViewCount}");
        Assert.Contains(inventory.Objects, o => o.ObjectType == "View" && o.Name == "vw_OrderSummary");
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsStoredProcedures()
    {
        var analyzer = new SchemaAnalyzer(_fixture.Connection);

        var inventory = await analyzer.AnalyzeAsync();

        Assert.True(inventory.StoredProcedureCount >= 3,
            $"Expected at least 3 stored procedures, got {inventory.StoredProcedureCount}");
        Assert.Contains(inventory.Objects,
            o => o.ObjectType == "StoredProcedure" && o.Name == "sp_GetUserOrders");
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsIndexes()
    {
        var analyzer = new SchemaAnalyzer(_fixture.Connection);

        var inventory = await analyzer.AnalyzeAsync();

        Assert.True(inventory.IndexCount >= 7, $"Expected at least 7 indexes, got {inventory.IndexCount}");
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsTriggers()
    {
        var analyzer = new SchemaAnalyzer(_fixture.Connection);

        var inventory = await analyzer.AnalyzeAsync();

        Assert.True(inventory.TriggerCount >= 1, $"Expected at least 1 trigger, got {inventory.TriggerCount}");
        Assert.Contains(inventory.Objects, o => o.ObjectType == "Trigger" && o.Name == "trg_AuditOrders");
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
