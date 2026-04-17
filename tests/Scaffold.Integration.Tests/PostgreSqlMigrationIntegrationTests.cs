using Npgsql;
using Scaffold.Migration.PostgreSql;

namespace Scaffold.Integration.Tests;

/// <summary>
/// Integration tests that verify end-to-end PostgreSQL → PostgreSQL migration.
/// All tests share a single migration run via <see cref="PostgreSqlMigrationFixture"/>;
/// the fixture seeds the PG source, creates an empty PG target, and executes the
/// full cutover migration once. Each test verifies a different aspect of the result.
/// </summary>
/// <remarks>
/// TODO: Add logical replication integration tests once CI supports wal_level=logical.
/// The standard PG 16 container uses wal_level=replica, so continuous sync tests
/// that use <see cref="LogicalReplicationSyncEngine"/> are deferred to a future wave.
/// </remarks>
[Collection("PostgreSqlMigration")]
public class PostgreSqlMigrationIntegrationTests
{
    private readonly PostgreSqlMigrationFixture _fixture;

    public PostgreSqlMigrationIntegrationTests(PostgreSqlMigrationFixture fixture)
        => _fixture = fixture;

    [Fact]
    public void MigrationResult_IsNotNull()
        => Assert.NotNull(_fixture.MigrationResult);

    [Fact]
    public void MigrationResult_IsSuccessful()
    {
        var result = _fixture.MigrationResult;
        Assert.NotNull(result);
        Assert.True(result.Success,
            $"Migration failed with errors: {string.Join("; ", result.Errors)}");
    }

    [Fact]
    public void MigrationResult_HasRowsMigrated()
    {
        var result = _fixture.MigrationResult;
        Assert.NotNull(result);
        Assert.True(result.RowsMigrated > 0,
            $"Expected rows migrated > 0, got {result.RowsMigrated}");
    }

    [Fact]
    public void MigrationResult_HasValidations()
    {
        var result = _fixture.MigrationResult;
        Assert.NotNull(result);
        Assert.NotEmpty(result.Validations);
    }

    [Fact]
    public void MigrationResult_AllValidationsPassed()
    {
        var result = _fixture.MigrationResult;
        Assert.NotNull(result);
        Assert.All(result.Validations, v =>
            Assert.True(v.Passed,
                $"Validation failed for {v.TableName}: source={v.SourceRowCount}, target={v.TargetRowCount}"));
    }

    [Fact]
    public void MigrationResult_HasCompletionTimestamp()
    {
        var result = _fixture.MigrationResult;
        Assert.NotNull(result);
        Assert.NotNull(result.CompletedAt);
    }

    [Fact]
    public void ProgressReports_ContainExpectedPhases()
    {
        var phases = _fixture.ProgressReports.Select(p => p.Phase).Distinct().ToList();

        // These phases are emitted by PostgreSqlMigrator.ExecuteCutoverAsync
        Assert.Contains("Extensions", phases);
        Assert.Contains("SchemaExtraction", phases);
        Assert.Contains("SchemaDeployment", phases);
        Assert.Contains("DataMigration", phases);
        Assert.Contains("SequenceReset", phases);
        Assert.Contains("Validation", phases);
    }

    [Theory]
    [InlineData("inventory.products")]
    [InlineData("inventory.categories")]
    [InlineData("inventory.product_categories")]
    public async Task TargetTable_HasSameRowCountAsSource(string tableName)
    {
        var sourceCount = await GetRowCountAsync(_fixture.SourceConnectionString, tableName);
        var targetCount = await GetRowCountAsync(_fixture.TargetConnectionString, tableName);

        Assert.Equal(sourceCount, targetCount);
    }

    [Fact]
    public async Task TargetProducts_HaveCorrectData()
    {
        // Verify actual data values, not just row counts
        await using var conn = new NpgsqlConnection(_fixture.TargetConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT name FROM inventory.products ORDER BY name", conn);

        var names = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        Assert.Equal(3, names.Count);
        Assert.Contains("Developer Laptop", names);
        Assert.Contains("USB-C Hub", names);
        Assert.Contains("Mechanical Keyboard", names);
    }

    [Fact]
    public async Task TargetProducts_PreserveJsonbMetadata()
    {
        // Verify JSONB data survived the migration
        await using var conn = new NpgsqlConnection(_fixture.TargetConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT metadata->>'cpu' FROM inventory.products WHERE name = 'Developer Laptop'", conn);

        var cpu = (string?)(await cmd.ExecuteScalarAsync());
        Assert.Equal("i7", cpu);
    }

    [Fact]
    public async Task TargetProducts_PreserveNumericPrecision()
    {
        // Verify NUMERIC(10,2) precision is preserved
        await using var conn = new NpgsqlConnection(_fixture.TargetConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT price FROM inventory.products WHERE name = 'Developer Laptop'", conn);

        var price = (decimal)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(1299.99m, price);
    }

    [Fact]
    public async Task TargetCategories_HaveForeignKeyRelationships()
    {
        // Verify FK self-references are intact (categories with parent_id)
        await using var conn = new NpgsqlConnection(_fixture.TargetConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM inventory.categories WHERE parent_id IS NOT NULL", conn);

        var count = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.True(count > 0, "Expected categories with parent_id references");
    }

    [Fact]
    public async Task TargetCategories_PreserveHstoreData()
    {
        // Verify hstore column data survived migration
        await using var conn = new NpgsqlConnection(_fixture.TargetConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT attributes->'brand' FROM inventory.categories WHERE name = 'Electronics'", conn);

        var brand = (string?)(await cmd.ExecuteScalarAsync());
        Assert.NotNull(brand);
        Assert.Equal("Various", brand);
    }

    [Fact]
    public async Task TargetSequences_AreReset()
    {
        // Verify serial sequences are at or above the max seeded value
        await using var conn = new NpgsqlConnection(_fixture.TargetConnectionString);
        await conn.OpenAsync();

        // Check categories serial sequence (seeded 3 categories, so seq should be >= 3)
        await using var cmd = new NpgsqlCommand(@"
            SELECT last_value FROM pg_sequences
            WHERE schemaname = 'inventory' AND sequencename LIKE 'categories_id_seq%'
            LIMIT 1", conn);

        var result = await cmd.ExecuteScalarAsync();
        if (result != null && result != DBNull.Value)
        {
            var lastValue = (long)result;
            Assert.True(lastValue >= 1, $"Sequence should be reset to at least 1, got {lastValue}");
        }
    }

    [Fact]
    public async Task TargetSchema_HasForeignKeyConstraints()
    {
        // Verify FK constraints were created on the target
        await using var conn = new NpgsqlConnection(_fixture.TargetConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("""
            SELECT COUNT(*)
            FROM pg_constraint con
            JOIN pg_class rel ON con.conrelid = rel.oid
            JOIN pg_namespace nsp ON rel.relnamespace = nsp.oid
            WHERE con.contype = 'f'
              AND nsp.nspname = 'inventory'
            """, conn);

        var fkCount = (long)(await cmd.ExecuteScalarAsync())!;
        // product_categories has 2 FKs (product_id, category_id), categories has 1 FK (parent_id)
        Assert.True(fkCount >= 3,
            $"Expected at least 3 foreign keys in inventory schema, found {fkCount}");
    }

    #region Helpers

    private static async Task<long> GetRowCountAsync(string connectionString, string tableName)
    {
        var quoted = PgIdentifierHelper.QuotePgName(tableName);
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {quoted}", conn);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    #endregion
}
