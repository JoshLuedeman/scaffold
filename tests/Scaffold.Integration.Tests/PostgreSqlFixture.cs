using Npgsql;

namespace Scaffold.Integration.Tests;

/// <summary>
/// Provides a shared PostgreSQL connection for integration tests.
/// Automatically creates the database and seeds it from postgres-seed.sql.
/// Connection string is read from the SCAFFOLD_TEST_PG_CONNECTION_STRING environment variable,
/// which is set by CI (GitHub Actions service container).
/// </summary>
public class PostgreSqlFixture : IAsyncLifetime
{
    private const string EnvVar = "SCAFFOLD_TEST_PG_CONNECTION_STRING";
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=scaffold_test;Username=postgres;Password=ScaffoldTest2025!";

    public string ConnectionString { get; }
    public NpgsqlConnection Connection { get; private set; } = null!;

    public PostgreSqlFixture()
    {
        ConnectionString = Environment.GetEnvironmentVariable(EnvVar) ?? DefaultConnectionString;
    }

    public async Task InitializeAsync()
    {
        // Connect to 'postgres' default database first to create test database
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString);
        var dbName = builder.Database;
        builder.Database = "postgres";

        await using var adminConn = new NpgsqlConnection(builder.ConnectionString);
        await adminConn.OpenAsync();

        // Create database if it doesn't exist
        await using var checkCmd = new NpgsqlCommand(
            $"SELECT 1 FROM pg_database WHERE datname = '{dbName}'", adminConn);
        var exists = await checkCmd.ExecuteScalarAsync();
        if (exists == null)
        {
            await using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{dbName}\"", adminConn);
            await createCmd.ExecuteNonQueryAsync();
        }
        await adminConn.CloseAsync();

        // Connect to test database
        Connection = new NpgsqlConnection(ConnectionString);
        await Connection.OpenAsync();

        // Seed from postgres-seed.sql
        var seedPath = Path.Combine(AppContext.BaseDirectory, "postgres-seed.sql");
        if (File.Exists(seedPath))
        {
            var sql = await File.ReadAllTextAsync(seedPath);
            await using var cmd = new NpgsqlCommand(sql, Connection);
            cmd.CommandTimeout = 60;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (Connection is { State: System.Data.ConnectionState.Open })
            await Connection.CloseAsync();
        Connection?.Dispose();
    }
}