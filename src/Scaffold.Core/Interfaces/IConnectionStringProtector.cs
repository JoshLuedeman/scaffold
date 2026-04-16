namespace Scaffold.Core.Interfaces;

public interface IConnectionStringProtector
{
    string Protect(string connectionString);
    string Unprotect(string protectedConnectionString);
}