using Microsoft.Data.SqlClient;

namespace Scaffold.Integration.Tests;

/// <summary>
/// Provides a shared SQL Server connection for integration tests.
/// Connection string is read from the SCAFFOLD_TEST_CONNECTION_STRING environment variable,
/// which is set by CI (GitHub Actions service container).
/// </summary>
public class SqlServerFixture : IAsyncLifetime
{
    private const string EnvVar = "SCAFFOLD_TEST_CONNECTION_STRING";
    private const string DefaultConnectionString =
        "Server=localhost,1433;Database=ScaffoldTestDb;User Id=sa;Password=ScaffoldTest2025!;TrustServerCertificate=true;Encrypt=true";

    public string ConnectionString { get; }
    public SqlConnection Connection { get; private set; } = null!;

    public SqlServerFixture()
    {
        ConnectionString = Environment.GetEnvironmentVariable(EnvVar) ?? DefaultConnectionString;
    }

    public async Task InitializeAsync()
    {
        Connection = new SqlConnection(ConnectionString);
        await Connection.OpenAsync();

        // Run seed script
        var seedPath = Path.Combine(AppContext.BaseDirectory, "seed-sample-db.sql");
        if (File.Exists(seedPath))
        {
            var sql = await File.ReadAllTextAsync(seedPath);
            // Split on GO batches
            var batches = sql.Split(["\nGO\n", "\nGO\r\n", "\r\nGO\r\n", "\r\nGO\n"],
                StringSplitOptions.RemoveEmptyEntries);

            foreach (var batch in batches)
            {
                var trimmed = batch.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                await using var cmd = new SqlCommand(trimmed, Connection);
                cmd.CommandTimeout = 30;
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (Connection is { State: System.Data.ConnectionState.Open })
            await Connection.CloseAsync();
        Connection?.Dispose();
    }
}
