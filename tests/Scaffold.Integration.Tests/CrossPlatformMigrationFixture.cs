using Microsoft.Data.SqlClient;
using Npgsql;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;
using Scaffold.Migration.PostgreSql;

namespace Scaffold.Integration.Tests;

/// <summary>
/// Fixture for cross-platform SQL Server → PostgreSQL migration integration tests.
/// Seeds a SQL Server source database, creates an empty PostgreSQL target,
/// runs the full migration once, and stores the result for all tests to verify.
/// Uses a dedicated SQL Server DB name to avoid conflicts with SqlServerFixture.
/// </summary>
public class CrossPlatformMigrationFixture : IAsyncLifetime
{
    private const string SqlEnvVar = "SCAFFOLD_TEST_CONNECTION_STRING";
    private const string PgEnvVar = "SCAFFOLD_TEST_PG_CONNECTION_STRING";

    private const string SourceDbName = "ScaffoldMigrationSourceDb";
    private const string TargetDbName = "scaffold_migration_target";

    private static readonly string DefaultSqlConnectionString =
        $"Server=localhost,1433;Database={SourceDbName};User Id=sa;Password=ScaffoldTest2025!;TrustServerCertificate=true;Encrypt=true";

    private static readonly string DefaultPgConnectionString =
        $"Host=localhost;Port=5432;Database={TargetDbName};Username=postgres;Password=ScaffoldTest2025!";

    /// <summary>
    /// The tables from the seed script that are included in the migration.
    /// </summary>
    public static readonly IReadOnlyList<string> MigratedTables = new[]
    {
        "dbo.Users", "dbo.Products", "dbo.Orders", "dbo.OrderItems", "dbo.AuditLog"
    };

    /// <summary>Source SQL Server connection string (seeded with test data).</summary>
    public string SourceConnectionString { get; }

    /// <summary>Target PostgreSQL connection string (migration target).</summary>
    public string TargetConnectionString { get; private set; } = string.Empty;

    /// <summary>Result of the single migration run, available to all tests.</summary>
    public MigrationResult? MigrationResult { get; private set; }

    /// <summary>All progress reports collected during the migration.</summary>
    public List<MigrationProgress> ProgressReports { get; } = new();

    public CrossPlatformMigrationFixture()
    {
        var envSql = Environment.GetEnvironmentVariable(SqlEnvVar);
        if (!string.IsNullOrEmpty(envSql))
        {
            // Override the database name to avoid conflicts with other fixtures
            var builder = new SqlConnectionStringBuilder(envSql);
            builder.InitialCatalog = SourceDbName;
            SourceConnectionString = builder.ConnectionString;
        }
        else
        {
            SourceConnectionString = DefaultSqlConnectionString;
        }
    }

    public async Task InitializeAsync()
    {
        // Step 1: Create and seed SQL Server source database
        await CreateAndSeedSqlServerAsync();

        // Step 2: Create empty PostgreSQL target database
        await CreatePostgreSqlTargetAsync();

        // Step 3: Run the full migration once; tests verify different aspects
        await RunMigrationAsync();
    }

    private async Task CreateAndSeedSqlServerAsync()
    {
        var builder = new SqlConnectionStringBuilder(SourceConnectionString);
        var dbName = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        await using var masterConn = new SqlConnection(builder.ConnectionString);
        await masterConn.OpenAsync();

        await using var createCmd = new SqlCommand(
            $"IF DB_ID('{dbName}') IS NULL CREATE DATABASE [{dbName}]", masterConn);
        createCmd.CommandTimeout = 30;
        await createCmd.ExecuteNonQueryAsync();
        await masterConn.CloseAsync();

        // Connect to the source database and seed it
        await using var conn = new SqlConnection(SourceConnectionString);
        await conn.OpenAsync();

        var seedPath = Path.Combine(AppContext.BaseDirectory, "seed-sample-db.sql");
        if (File.Exists(seedPath))
        {
            var sql = await File.ReadAllTextAsync(seedPath);
            var batches = sql.Split(
                ["\nGO\n", "\nGO\r\n", "\r\nGO\r\n", "\r\nGO\n"],
                StringSplitOptions.RemoveEmptyEntries);

            foreach (var batch in batches)
            {
                var trimmed = batch.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                await using var cmd = new SqlCommand(trimmed, conn);
                cmd.CommandTimeout = 30;
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    private async Task CreatePostgreSqlTargetAsync()
    {
        // Build PG admin connection from env var or default
        var envPg = Environment.GetEnvironmentVariable(PgEnvVar);
        NpgsqlConnectionStringBuilder builder;

        if (!string.IsNullOrEmpty(envPg))
        {
            builder = new NpgsqlConnectionStringBuilder(envPg);
        }
        else
        {
            builder = new NpgsqlConnectionStringBuilder(DefaultPgConnectionString);
        }

        // Connect to 'postgres' default database to create target
        builder.Database = "postgres";

        await using var adminConn = new NpgsqlConnection(builder.ConnectionString);
        await adminConn.OpenAsync();

        // Drop and recreate to ensure clean state
        await using (var terminateCmd = new NpgsqlCommand(
            $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{TargetDbName}' AND pid <> pg_backend_pid()",
            adminConn))
        {
            await terminateCmd.ExecuteNonQueryAsync();
        }

        await using (var dropCmd = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{TargetDbName}\"", adminConn))
        {
            await dropCmd.ExecuteNonQueryAsync();
        }

        await using (var createCmd = new NpgsqlCommand(
            $"CREATE DATABASE \"{TargetDbName}\"", adminConn))
        {
            await createCmd.ExecuteNonQueryAsync();
        }

        await adminConn.CloseAsync();

        // Set the target connection string
        builder.Database = TargetDbName;
        TargetConnectionString = builder.ConnectionString;
    }

    private async Task RunMigrationAsync()
    {
        var plan = new MigrationPlan
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            SourcePlatform = DatabasePlatform.SqlServer,
            TargetPlatform = DatabasePlatform.PostgreSql,
            Strategy = MigrationStrategy.Cutover,
            IncludedObjects = MigratedTables.ToList(),
            SourceConnectionString = SourceConnectionString,
            UseExistingTarget = true,
            ExistingTargetConnectionString = TargetConnectionString,
            PreMigrationScripts = new List<MigrationScript>
            {
                new()
                {
                    ScriptId = "pre-1",
                    Label = "Create migration metadata table",
                    ScriptType = MigrationScriptType.Custom,
                    Phase = MigrationScriptPhase.Pre,
                    SqlContent = "CREATE TABLE IF NOT EXISTS public.migration_metadata (key TEXT PRIMARY KEY, value TEXT);",
                    IsEnabled = true,
                    Order = 1
                }
            },
            PostMigrationScripts = new List<MigrationScript>
            {
                new()
                {
                    ScriptId = "post-1",
                    Label = "Record migration completion",
                    ScriptType = MigrationScriptType.Custom,
                    Phase = MigrationScriptPhase.Post,
                    SqlContent = "INSERT INTO public.migration_metadata (key, value) VALUES ('migration_complete', 'true');",
                    IsEnabled = true,
                    Order = 1
                }
            }
        };

        // Use a synchronous progress reporter to avoid race conditions
        // with Progress<T>'s async thread pool callbacks
        var progress = new SynchronousProgress(ProgressReports);
        var migrator = new SqlServerToPostgreSqlMigrator();

        MigrationResult = await migrator.ExecuteCutoverAsync(plan, progress);
    }

    public async Task DisposeAsync()
    {
        // Best-effort cleanup: drop the PG target database
        try
        {
            if (string.IsNullOrEmpty(TargetConnectionString)) return;

            var builder = new NpgsqlConnectionStringBuilder(TargetConnectionString);
            builder.Database = "postgres";

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