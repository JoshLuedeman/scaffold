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
}
