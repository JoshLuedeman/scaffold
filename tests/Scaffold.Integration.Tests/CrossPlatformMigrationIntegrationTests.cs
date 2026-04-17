using Microsoft.Data.SqlClient;
using Npgsql;
using Scaffold.Core.Interfaces;
using Scaffold.Migration.PostgreSql;

namespace Scaffold.Integration.Tests;

/// <summary>
/// Integration tests that verify end-to-end SQL Server → PostgreSQL migration.
/// All tests share a single migration run via CrossPlatformMigrationFixture;
/// the fixture seeds SQL Server, creates an empty PG target, and executes the
/// full cutover migration once. Each test verifies a different aspect of the result.
/// </summary>
[Collection("CrossPlatformMigration")]
public class CrossPlatformMigrationIntegrationTests
{
    private readonly CrossPlatformMigrationFixture _fixture;

    public CrossPlatformMigrationIntegrationTests(CrossPlatformMigrationFixture fixture)
        => _fixture = fixture;

    [Fact]
    public void FullCutoverMigration_SuccessfullyMigratesData()
    {
        var result = _fixture.MigrationResult;

        Assert.NotNull(result);
        Assert.True(result.Success,
            $"Migration failed with errors: {string.Join("; ", result.Errors)}");
        Assert.True(result.RowsMigrated > 0,
            $"Expected rows migrated > 0, got {result.RowsMigrated}");
        Assert.NotNull(result.CompletedAt);
    }

    [Fact]
    public async Task CutoverMigration_DeploysSchemaToPostgreSql()
    {
        var expectedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Users", "Products", "Orders", "OrderItems", "AuditLog"
        };

        await using var conn = new NpgsqlConnection(_fixture.TargetConnectionString);
        await conn.OpenAsync();

        var actualTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'",
            conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            actualTables.Add(reader.GetString(0));
        }

        foreach (var expected in expectedTables)
        {
            Assert.Contains(expected, actualTables);
        }
    }

    [Fact]
    public async Task CutoverMigration_RowCountsMatchSourceAndTarget()
    {
        foreach (var table in CrossPlatformMigrationFixture.MigratedTables)
        {
            var sourceCount = await GetSqlServerRowCountAsync(table);
            var targetCount = await GetPostgreSqlRowCountAsync(table);

            Assert.Equal(sourceCount, targetCount);
        }
    }

    [Fact]
    public async Task CutoverMigration_PreservesDataTypeValues()
    {
        await using var conn = new NpgsqlConnection(_fixture.TargetConnectionString);
        await conn.OpenAsync();

        // Verify int values (Users.Id should be 1, 2, 3)
        await using (var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM public.\"Users\" WHERE \"Id\" BETWEEN 1 AND 3", conn))
        {
            var count = (long)(await cmd.ExecuteScalarAsync())!;
            Assert.Equal(3, count);
        }

        // Verify varchar/nvarchar text (Users.Username preserved)
        await using (var cmd = new NpgsqlCommand(
            "SELECT \"Username\" FROM public.\"Users\" WHERE \"Id\" = 1", conn))
        {
            var username = (string)(await cmd.ExecuteScalarAsync())!;
            Assert.Equal("alice", username);
        }

        // Verify decimal precision (Products.Price = 9.99)
        await using (var cmd = new NpgsqlCommand(
            "SELECT \"Price\" FROM public.\"Products\" WHERE \"Name\" = 'Widget A'", conn))
        {
            var price = (decimal)(await cmd.ExecuteScalarAsync())!;
            Assert.Equal(9.99m, price);
        }

        // Verify bit → boolean (Users.IsActive = true for user 1)
        await using (var cmd = new NpgsqlCommand(
            "SELECT \"IsActive\" FROM public.\"Users\" WHERE \"Id\" = 1", conn))
        {
            var isActive = (bool)(await cmd.ExecuteScalarAsync())!;
            Assert.True(isActive);
        }

        // Verify datetime2 → timestamp (Users.CreatedAt is a valid timestamp)
        await using (var cmd = new NpgsqlCommand(
            "SELECT \"CreatedAt\" FROM public.\"Users\" WHERE \"Id\" = 1", conn))
        {
            var createdAt = await cmd.ExecuteScalarAsync();
            Assert.NotNull(createdAt);
            Assert.IsType<DateTime>(createdAt);
        }

        // Verify nvarchar(max) → text (Products.Description preserved)
        await using (var cmd = new NpgsqlCommand(
            "SELECT \"Description\" FROM public.\"Products\" WHERE \"Name\" = 'Widget A'", conn))
        {
            var description = (string)(await cmd.ExecuteScalarAsync())!;
            Assert.Equal("A standard widget", description);
        }

        // Verify nvarchar status (Orders.Status preserved)
        await using (var cmd = new NpgsqlCommand(
            "SELECT \"Status\" FROM public.\"Orders\" WHERE \"Id\" = 1", conn))
        {
            var status = (string)(await cmd.ExecuteScalarAsync())!;
            Assert.Equal("Completed", status);
        }

        // Verify int quantity and decimal unit price (OrderItems preserved)
        await using (var cmd = new NpgsqlCommand(
            "SELECT \"Quantity\", \"UnitPrice\" FROM public.\"OrderItems\" WHERE \"Id\" = 1", conn))
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(2, reader.GetInt32(0));
            Assert.Equal(9.99m, reader.GetDecimal(1));
        }
    }

    [Fact]
    public async Task CutoverMigration_CreatesForeignKeysOnTarget()
    {
        await using var conn = new NpgsqlConnection(_fixture.TargetConnectionString);
        await conn.OpenAsync();

        // Query pg_constraint for FK constraints in the public schema
        await using var cmd = new NpgsqlCommand("""
            SELECT
                con.conname AS constraint_name,
                rel.relname AS table_name,
                ref.relname AS referenced_table
            FROM pg_constraint con
            JOIN pg_class rel ON con.conrelid = rel.oid
            JOIN pg_class ref ON con.confrelid = ref.oid
            JOIN pg_namespace nsp ON rel.relnamespace = nsp.oid
            WHERE con.contype = 'f'
              AND nsp.nspname = 'public'
            """, conn);

        var foreignKeys = new List<(string Name, string Table, string ReferencedTable)>();
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            foreignKeys.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        // Expect FKs: Orders→Users, OrderItems→Orders, OrderItems→Products
        Assert.True(foreignKeys.Count >= 3,
            $"Expected at least 3 foreign keys, found {foreignKeys.Count}: [{string.Join(", ", foreignKeys.Select(fk => $"{fk.Table}→{fk.ReferencedTable}"))}]");

        Assert.Contains(foreignKeys, fk =>
            fk.Table.Equals("Orders", StringComparison.OrdinalIgnoreCase) &&
            fk.ReferencedTable.Equals("Users", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(foreignKeys, fk =>
            fk.Table.Equals("OrderItems", StringComparison.OrdinalIgnoreCase) &&
            fk.ReferencedTable.Equals("Orders", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(foreignKeys, fk =>
            fk.Table.Equals("OrderItems", StringComparison.OrdinalIgnoreCase) &&
            fk.ReferencedTable.Equals("Products", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidationEngine_ValidatesSuccessfulMigration()
    {
        // Run the validation engine independently against source and target
        var validationEngine = new PostgreSqlValidationEngine();
        var summary = await validationEngine.ValidateAsync(
            _fixture.SourceConnectionString,
            _fixture.TargetConnectionString,
            CrossPlatformMigrationFixture.MigratedTables);

        Assert.True(summary.AllPassed,
            $"Validation failed: {summary.TablesFailed} of {summary.TablesValidated} tables failed. " +
            $"Details: {string.Join("; ", summary.Results.Where(r => !r.Passed).Select(r => $"{r.TableName}: source={r.SourceRowCount}, target={r.TargetRowCount}"))}");
        Assert.Equal(CrossPlatformMigrationFixture.MigratedTables.Count, summary.TablesValidated);
        Assert.Equal(0, summary.TablesFailed);
    }

    [Fact]
    public async Task CutoverMigration_ExecutesPrePostScripts()
    {
        await using var conn = new NpgsqlConnection(_fixture.TargetConnectionString);
        await conn.OpenAsync();

        // Verify pre-script created the migration_metadata table
        await using (var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'migration_metadata'",
            conn))
        {
            var tableCount = (long)(await cmd.ExecuteScalarAsync())!;
            Assert.Equal(1, tableCount);
        }

        // Verify post-script inserted the completion record
        await using (var cmd = new NpgsqlCommand(
            "SELECT value FROM public.migration_metadata WHERE key = 'migration_complete'",
            conn))
        {
            var value = (string)(await cmd.ExecuteScalarAsync())!;
            Assert.Equal("true", value);
        }
    }

    [Fact]
    public void CutoverMigration_ReportsProgressThroughAllPhases()
    {
        var reports = _fixture.ProgressReports;
        Assert.NotEmpty(reports);

        var phases = reports.Select(r => r.Phase).Distinct().ToHashSet();

        // Verify all expected migration phases are reported
        Assert.Contains("SchemaDeployment", phases);
        Assert.Contains("PreScripts", phases);
        Assert.Contains("DataMigration", phases);
        Assert.Contains("PostScripts", phases);
        Assert.Contains("Validation", phases);
    }

    #region Helpers

    /// <summary>
    /// Gets the row count from the SQL Server source for a given table.
    /// </summary>
    private async Task<long> GetSqlServerRowCountAsync(string tableName)
    {
        var parts = tableName.Split('.');
        var quotedName = string.Join(".", parts.Select(p => $"[{p.Trim('[', ']')}]"));

        await using var conn = new SqlConnection(_fixture.SourceConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand($"SELECT COUNT_BIG(*) FROM {quotedName}", conn);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// Gets the row count from the PostgreSQL target for a given table.
    /// Maps "dbo" schema to "public" for PostgreSQL.
    /// </summary>
    private async Task<long> GetPostgreSqlRowCountAsync(string tableName)
    {
        var parts = tableName.Split('.');
        if (parts.Length == 2 &&
            parts[0].Trim('"', '[', ']').Equals("dbo", StringComparison.OrdinalIgnoreCase))
        {
            parts[0] = "public";
        }

        var quotedName = string.Join(".", parts.Select(p => $"\"{p.Trim('[', ']', '\"')}\""));

        await using var conn = new NpgsqlConnection(_fixture.TargetConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {quotedName}", conn);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    #endregion
}