using Npgsql;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;
using Scaffold.Migration.PostgreSql;

namespace Scaffold.Integration.Tests;

/// <summary>
/// Fixture for PostgreSQL → PostgreSQL migration integration tests.
/// Reuses the seeded <c>scaffold_test</c> source database, creates an empty
/// <c>scaffold_migration_pg_target</c> target, and runs the full PG→PG cutover
/// migration once. Each test verifies a different aspect of the result.
/// </summary>
public class PostgreSqlMigrationFixture : IAsyncLifetime
{
    private const string PgEnvVar = "SCAFFOLD_TEST_PG_CONNECTION_STRING";
    private const string SourceDbName = "scaffold_test";
    private const string TargetDbName = "scaffold_migration_pg_target";

    private static readonly string DefaultSourceConnectionString =
        $"Host=localhost;Port=5432;Database={SourceDbName};Username=postgres;Password=ScaffoldTest2025!";

    /// <summary>
    /// Source PG tables included in the migration (from postgres-seed.sql).
    /// </summary>
    public static readonly IReadOnlyList<string> MigratedTables = new[]
    {
        "inventory.products",
        "inventory.categories",
        "inventory.product_categories"
    };

    public string SourceConnectionString { get; }
    public string TargetConnectionString { get; private set; } = string.Empty;
    public MigrationResult? MigrationResult { get; private set; }
    public List<MigrationProgress> ProgressReports { get; } = new();

    public PostgreSqlMigrationFixture()
    {
        var env = Environment.GetEnvironmentVariable(PgEnvVar);
        if (!string.IsNullOrEmpty(env))
        {
            var builder = new NpgsqlConnectionStringBuilder(env) { Database = SourceDbName };
            SourceConnectionString = builder.ConnectionString;
        }
        else
        {
            SourceConnectionString = DefaultSourceConnectionString;
        }
    }

    public async Task InitializeAsync()
    {
        // Ensure source DB exists and is seeded (PostgreSqlFixture may have done this already)
        await SeedSourceAsync();

        // Create empty target database
        await CreateTargetDatabaseAsync();

        // Run the full PG→PG cutover migration
        await RunMigrationAsync();
    }

    private async Task SeedSourceAsync()
    {
        // Connect to postgres admin DB to ensure source database exists
        var builder = new NpgsqlConnectionStringBuilder(SourceConnectionString) { Database = "postgres" };
        await using var adminConn = new NpgsqlConnection(builder.ConnectionString);
        await adminConn.OpenAsync();

        await using var checkCmd = new NpgsqlCommand(
            $"SELECT 1 FROM pg_database WHERE datname = '{SourceDbName}'", adminConn);
        var exists = await checkCmd.ExecuteScalarAsync();
        if (exists == null)
        {
            await using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{SourceDbName}\"", adminConn);
            await createCmd.ExecuteNonQueryAsync();
        }
        await adminConn.CloseAsync();

        // Only seed if not already seeded (the seed SQL is not idempotent for table creation)
        await using var conn = new NpgsqlConnection(SourceConnectionString);
        await conn.OpenAsync();

        await using var seedCheck = new NpgsqlCommand(
            "SELECT 1 FROM information_schema.tables WHERE table_schema = 'inventory' AND table_name = 'products'",
            conn);
        var alreadySeeded = await seedCheck.ExecuteScalarAsync();
        if (alreadySeeded != null) return;

        var seedPath = Path.Combine(AppContext.BaseDirectory, "postgres-seed.sql");
        if (File.Exists(seedPath))
        {
            var sql = await File.ReadAllTextAsync(seedPath);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.CommandTimeout = 60;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task CreateTargetDatabaseAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(SourceConnectionString) { Database = "postgres" };
        await using var adminConn = new NpgsqlConnection(builder.ConnectionString);
        await adminConn.OpenAsync();

        // Terminate existing connections for clean state
        await using (var terminateCmd = new NpgsqlCommand(
            $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{TargetDbName}' AND pid <> pg_backend_pid()",
            adminConn))
        {
            await terminateCmd.ExecuteNonQueryAsync();
        }

        // Drop if exists for clean state
        await using (var dropCmd = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{TargetDbName}\"", adminConn))
        {
            await dropCmd.ExecuteNonQueryAsync();
        }

        await using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{TargetDbName}\"", adminConn);
        await createCmd.ExecuteNonQueryAsync();
        await adminConn.CloseAsync();

        var targetBuilder = new NpgsqlConnectionStringBuilder(SourceConnectionString) { Database = TargetDbName };
        TargetConnectionString = targetBuilder.ConnectionString;
    }

    private async Task RunMigrationAsync()
    {
        var plan = new MigrationPlan
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            SourcePlatform = DatabasePlatform.PostgreSql,
            TargetPlatform = DatabasePlatform.PostgreSql,
            Strategy = MigrationStrategy.Cutover,
            SourceConnectionString = SourceConnectionString,
            UseExistingTarget = true,
            ExistingTargetConnectionString = TargetConnectionString,
            IncludedObjects = MigratedTables.ToList()
        };

        // Use synchronous progress reporter to avoid race conditions
        // with Progress<T>'s async thread pool callbacks
        var progress = new SynchronousProgress(ProgressReports);

        var schemaExtractor = new PostgreSqlSchemaExtractor();
        var ddlGenerator = new PostgreSqlDdlGenerator();
        var bulkCopier = new PostgreSqlBulkCopier();
        var scriptExecutor = new PostgreSqlScriptExecutor();
        var validationEngine = new PostgreSqlToPostgreSqlValidationEngine();
        var extensionHandler = new AzureExtensionHandler();

        var migrator = new PostgreSqlMigrator(
            schemaExtractor, ddlGenerator, bulkCopier,
            scriptExecutor, validationEngine, extensionHandler);

        MigrationResult = await migrator.ExecuteCutoverAsync(plan, progress);
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(TargetConnectionString)) return;

            var builder = new NpgsqlConnectionStringBuilder(SourceConnectionString) { Database = "postgres" };
            await using var adminConn = new NpgsqlConnection(builder.ConnectionString);
            await adminConn.OpenAsync();

            await using var terminateCmd = new NpgsqlCommand(
                $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{TargetDbName}' AND pid <> pg_backend_pid()",
                adminConn);
            await terminateCmd.ExecuteNonQueryAsync();

            await using var dropCmd = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS \"{TargetDbName}\"", adminConn);
            await dropCmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best-effort cleanup; CI containers are ephemeral anyway
        }
    }

    /// <summary>
    /// Synchronous IProgress implementation that adds reports immediately
    /// instead of posting to the SynchronizationContext like Progress&lt;T&gt; does.
    /// This ensures all reports are captured before InitializeAsync returns.
    /// </summary>
    private sealed class SynchronousProgress : IProgress<MigrationProgress>
    {
        private readonly List<MigrationProgress> _reports;

        public SynchronousProgress(List<MigrationProgress> reports)
            => _reports = reports;

        public void Report(MigrationProgress value)
            => _reports.Add(value);
    }
}
