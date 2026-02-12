using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests;

public class ConnectionInfoTests
{
    [Fact]
    public void BuildConnectionString_SqlAuth_IncludesCredentials()
    {
        var info = new ConnectionInfo
        {
            Server = "myserver.database.windows.net",
            Database = "MyDb",
            Port = 1433,
            UseSqlAuthentication = true,
            Username = "admin",
            Password = "secret123",
            TrustServerCertificate = true
        };

        var connStr = info.BuildConnectionString();

        Assert.Contains("myserver.database.windows.net", connStr);
        Assert.Contains("MyDb", connStr);
        Assert.Contains("admin", connStr);
        Assert.Contains("secret123", connStr);
    }

    [Fact]
    public void BuildConnectionString_NonDefaultPort_IncludesPort()
    {
        var info = new ConnectionInfo
        {
            Server = "localhost",
            Database = "TestDb",
            Port = 1444,
            UseSqlAuthentication = true,
            Username = "sa",
            Password = "pass"
        };

        var connStr = info.BuildConnectionString();
        Assert.Contains("localhost,1444", connStr);
    }

    [Fact]
    public void BuildConnectionString_WindowsAuth_NoCredentials()
    {
        var info = new ConnectionInfo
        {
            Server = "localhost",
            Database = "TestDb",
            Port = 1433,
            UseSqlAuthentication = false
        };

        var connStr = info.BuildConnectionString();
        Assert.Contains("Integrated Security=True", connStr);
        Assert.DoesNotContain("User ID", connStr);
    }
}
