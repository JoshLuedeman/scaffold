using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Data.SqlClient;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.SqlServer;

public class SqlServerConnectionFactory
{
    public async Task<SqlConnection> CreateConnectionAsync(ConnectionInfo info)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = info.Port == 1433 ? info.Server : $"{info.Server},{info.Port}",
            InitialCatalog = info.Database,
            TrustServerCertificate = info.TrustServerCertificate,
            Encrypt = true
        };

        if (info.UseSqlAuthentication)
        {
            builder.UserID = info.Username;
            builder.Password = await ResolvePasswordAsync(info.Password, info.KeyVaultSecretUri);
            builder.IntegratedSecurity = false;
        }
        else
        {
            builder.IntegratedSecurity = true;
        }

        var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<string> ResolvePasswordAsync(string? password, string? keyVaultSecretUri)
    {
        // Use direct password if provided (local/dev scenarios)
        if (!string.IsNullOrWhiteSpace(password))
            return password;

        if (string.IsNullOrWhiteSpace(keyVaultSecretUri))
            throw new InvalidOperationException(
                "Either Password or KeyVaultSecretUri is required when using SQL authentication.");

        var secretUri = new Uri(keyVaultSecretUri);
        var client = new SecretClient(
            new Uri($"{secretUri.Scheme}://{secretUri.Host}"),
            new DefaultAzureCredential());

        // The last segment of the URI is the secret name
        var secretName = secretUri.Segments[^1].TrimEnd('/');
        var secret = await client.GetSecretAsync(secretName);
        return secret.Value.Value;
    }
}
