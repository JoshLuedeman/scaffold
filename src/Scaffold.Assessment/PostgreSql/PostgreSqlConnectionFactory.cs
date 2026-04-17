using Npgsql;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.PostgreSql;

public class PostgreSqlConnectionFactory
{
    public async Task<NpgsqlConnection> CreateConnectionAsync(ConnectionInfo info)
    {
        var builder = await BuildConnectionStringBuilderAsync(info);

        var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>
    /// Builds an NpgsqlConnectionStringBuilder from the given ConnectionInfo.
    /// Extracted for testability — unit tests can verify connection string generation
    /// without opening a real database connection.
    /// </summary>
    internal async Task<NpgsqlConnectionStringBuilder> BuildConnectionStringBuilderAsync(ConnectionInfo info)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = info.Server,
            Port = info.Port == 1433 ? 5432 : info.Port,  // Use PG default if SQL default was left
            Database = info.Database,
            SslMode = info.TrustServerCertificate ? SslMode.Prefer : SslMode.Require
        };

        if (!string.IsNullOrEmpty(info.Username))
        {
            builder.Username = info.Username;
            builder.Password = await ResolvePasswordAsync(info.Password, info.KeyVaultSecretUri);
        }
        else
        {
            // Use integrated/Azure AD auth — no credentials set
            builder.Username = info.Username;
        }

        return builder;
    }

    private static async Task<string> ResolvePasswordAsync(string? password, string? keyVaultSecretUri)
    {
        if (!string.IsNullOrWhiteSpace(password))
            return password;

        if (string.IsNullOrWhiteSpace(keyVaultSecretUri))
            throw new InvalidOperationException(
                "Either Password or KeyVaultSecretUri is required for PostgreSQL authentication.");

        // Reuse same Key Vault pattern as SQL Server factory
        var secretUri = new Uri(keyVaultSecretUri);
        var client = new Azure.Security.KeyVault.Secrets.SecretClient(
            new Uri($"{secretUri.Scheme}://{secretUri.Host}"),
            new Azure.Identity.DefaultAzureCredential());
        var secretName = secretUri.Segments[^1].TrimEnd('/');
        var secret = await client.GetSecretAsync(secretName);
        return secret.Value.Value;
    }
}
