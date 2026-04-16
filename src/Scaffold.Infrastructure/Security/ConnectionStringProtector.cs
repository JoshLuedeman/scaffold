using Microsoft.AspNetCore.DataProtection;
using Scaffold.Core.Interfaces;

namespace Scaffold.Infrastructure.Security;

public class ConnectionStringProtector : IConnectionStringProtector
{
    private readonly IDataProtector _protector;

    public ConnectionStringProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Scaffold.ConnectionStrings");
    }

    public string Protect(string connectionString) => _protector.Protect(connectionString);
    public string Unprotect(string protectedConnectionString) => _protector.Unprotect(protectedConnectionString);
}