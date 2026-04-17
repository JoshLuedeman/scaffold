using Scaffold.Migration.PostgreSql;

namespace Scaffold.Integration.Tests;

/// <summary>
/// Integration tests for <see cref="PostgreSqlSchemaExtractor"/> against a real PostgreSQL instance.
/// Validates that the extractor correctly reads tables, columns, indexes, sequences,
/// views, functions, and extensions from pg_catalog and information_schema.
/// </summary>
[Collection("PostgreSql")]
public class PostgreSqlSchemaExtractorIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    public PostgreSqlSchemaExtractorIntegrationTests(PostgreSqlFixture fixture)
        => _fixture = fixture;

    [Fact]
    public async Task ExtractSchema_ReturnsTables()
    {
        var extractor = new PostgreSqlSchemaExtractor();
        var snapshot = await extractor.ExtractSchemaAsync(_fixture.ConnectionString);

        Assert.NotEmpty(snapshot.Tables);
        // The seed creates at least: inventory.products, inventory.categories, inventory.product_categories
        Assert.True(snapshot.Tables.Count >= 3,
            $"Expected at least 3 tables, got {snapshot.Tables.Count}");
    }

    [Fact]
    public async Task ExtractSchema_ReturnsIndexes()
    {
        var extractor = new PostgreSqlSchemaExtractor();
        var snapshot = await extractor.ExtractSchemaAsync(_fixture.ConnectionString);

        // Indexes live on tables, not at the snapshot root
        var allIndexes = snapshot.Tables.SelectMany(t => t.Indexes).ToList();
        Assert.NotEmpty(allIndexes);
    }

    [Fact]
    public async Task ExtractSchema_ReturnsSequences()
    {
        var extractor = new PostgreSqlSchemaExtractor();
        var snapshot = await extractor.ExtractSchemaAsync(_fixture.ConnectionString);

        Assert.NotEmpty(snapshot.Sequences);
        // The seed creates inventory.order_seq explicitly + categories_id_seq via SERIAL
        Assert.True(snapshot.Sequences.Count >= 1,
            $"Expected at least 1 sequence, got {snapshot.Sequences.Count}");
    }

    [Fact]
    public async Task ExtractSchema_ReturnsViews()
    {
        var extractor = new PostgreSqlSchemaExtractor();
        var snapshot = await extractor.ExtractSchemaAsync(_fixture.ConnectionString);

        Assert.NotEmpty(snapshot.Views);
        // The seed creates inventory.product_summary (regular) and analytics.daily_views (materialized)
    }

    [Fact]
    public async Task ExtractSchema_ReturnsFunctions()
    {
        var extractor = new PostgreSqlSchemaExtractor();
        var snapshot = await extractor.ExtractSchemaAsync(_fixture.ConnectionString);

        Assert.NotEmpty(snapshot.Functions);
        // The seed creates inventory.update_timestamp() trigger function
    }

    [Fact]
    public async Task ExtractSchema_ReturnsExtensions()
    {
        var extractor = new PostgreSqlSchemaExtractor();
        var snapshot = await extractor.ExtractSchemaAsync(_fixture.ConnectionString);

        Assert.NotEmpty(snapshot.Extensions);
        // The seed enables uuid-ossp, pg_trgm, and hstore
        Assert.Contains(snapshot.Extensions, e => e == "uuid-ossp" || e == "pg_trgm" || e == "hstore");
    }

    [Fact]
    public async Task ExtractSchema_WithTableFilter_ReturnsOnlyFilteredTables()
    {
        var extractor = new PostgreSqlSchemaExtractor();
        var filter = new List<string> { "inventory.products" };

        var snapshot = await extractor.ExtractSchemaAsync(_fixture.ConnectionString, filter);

        Assert.Single(snapshot.Tables);
        Assert.Equal("products", snapshot.Tables[0].TableName);
        Assert.Equal("inventory", snapshot.Tables[0].Schema);
    }

    [Fact]
    public async Task ExtractSchema_ProductsTable_HasExpectedColumns()
    {
        var extractor = new PostgreSqlSchemaExtractor();
        var filter = new List<string> { "inventory.products" };

        var snapshot = await extractor.ExtractSchemaAsync(_fixture.ConnectionString, filter);
        var products = snapshot.Tables.First();

        // PgColumnDefinition uses .Name, not .ColumnName
        var colNames = products.Columns.Select(c => c.Name).ToList();
        Assert.Contains("id", colNames);
        Assert.Contains("name", colNames);
        Assert.Contains("price", colNames);
        Assert.Contains("metadata", colNames);
        Assert.Contains("tags", colNames);
        Assert.Contains("created_at", colNames);
        Assert.Contains("updated_at", colNames);
    }

    [Fact]
    public async Task ExtractSchema_ProductsTable_HasPrimaryKey()
    {
        var extractor = new PostgreSqlSchemaExtractor();
        var filter = new List<string> { "inventory.products" };

        var snapshot = await extractor.ExtractSchemaAsync(_fixture.ConnectionString, filter);
        var products = snapshot.Tables.First();

        Assert.NotNull(products.PrimaryKey);
        Assert.Contains("id", products.PrimaryKey.Columns);
    }

    [Fact]
    public async Task ExtractSchema_CategoriesTable_HasForeignKeys()
    {
        var extractor = new PostgreSqlSchemaExtractor();
        var filter = new List<string> { "inventory.categories" };

        var snapshot = await extractor.ExtractSchemaAsync(_fixture.ConnectionString, filter);
        var categories = snapshot.Tables.First();

        // categories has a self-referencing FK on parent_id
        Assert.NotEmpty(categories.ForeignKeys);
        Assert.Contains(categories.ForeignKeys, fk =>
            fk.Columns.Contains("parent_id") && fk.ReferencedTable == "categories");
    }

    [Fact]
    public async Task ExtractSchema_MultipleTables_ReturnCorrectCount()
    {
        var extractor = new PostgreSqlSchemaExtractor();
        var filter = new List<string>
        {
            "inventory.products",
            "inventory.categories",
            "inventory.product_categories"
        };

        var snapshot = await extractor.ExtractSchemaAsync(_fixture.ConnectionString, filter);

        Assert.Equal(3, snapshot.Tables.Count);
        var tableNames = snapshot.Tables.Select(t => t.TableName).ToHashSet();
        Assert.Contains("products", tableNames);
        Assert.Contains("categories", tableNames);
        Assert.Contains("product_categories", tableNames);
    }
}
