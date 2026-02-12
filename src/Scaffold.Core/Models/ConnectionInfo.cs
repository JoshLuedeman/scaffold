using System.ComponentModel.DataAnnotations.Schema;

namespace Scaffold.Core.Models;

public class ConnectionInfo
{
    public Guid Id { get; set; }
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public int Port { get; set; } = 1433;
    public bool UseSqlAuthentication { get; set; }
    public string? Username { get; set; }
    public string? KeyVaultSecretUri { get; set; }
    public bool TrustServerCertificate { get; set; }

    /// <summary>
    /// Runtime-only password for local/dev use. Not persisted to the database.
    /// When set, takes precedence over KeyVaultSecretUri.
    /// </summary>
    [NotMapped]
    public string? Password { get; set; }

    public string BuildConnectionString()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = Port == 1433 ? Server : $"{Server},{Port}",
            InitialCatalog = Database,
            TrustServerCertificate = TrustServerCertificate,
            Encrypt = true
        };

        if (UseSqlAuthentication)
        {
            builder.UserID = Username;
            builder.Password = Password;
            builder.IntegratedSecurity = false;
        }
        else
        {
            builder.IntegratedSecurity = true;
        }

        return builder.ConnectionString;
    }
}
