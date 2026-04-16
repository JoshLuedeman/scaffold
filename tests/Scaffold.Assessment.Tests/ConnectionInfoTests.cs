using Scaffold.Core.Enums;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests;

public class ConnectionInfoTests
{
    // -- Platform property defaults --

    [Fact]
    public void Platform_DefaultsToSqlServer()
    {
        var info = new ConnectionInfo();
        Assert.Equal(DatabasePlatform.SqlServer, info.Platform);
    }

    // -- DefaultPortFor --

    [Theory]
    [InlineData(DatabasePlatform.SqlServer, 1433)]
    [InlineData(DatabasePlatform.PostgreSql, 5432)]
    public void DefaultPortFor_ReturnsExpectedPort(DatabasePlatform platform, int expectedPort)
    {
        Assert.Equal(expectedPort, ConnectionInfo.DefaultPortFor(platform));
    }

    // -- SQL Server connection strings --

    [Fact]
    public void BuildConnectionString_SqlServer_SqlAuth_IncludesCredentials()
    {
        var info = new ConnectionInfo
        {
            Server = "myserver.database.windows.net",
            Database = "MyDb",
            Port = 1433,
            UseSqlAuthentication = true,
            Username = "admin",
            Password = "secret123",
            TrustServerCertificate = true,
            Platform = DatabasePlatform.SqlServer
        };

        var connStr = info.BuildConnectionString();

        Assert.Contains("myserver.database.windows.net", connStr);
        Assert.Contains("MyDb", connStr);
        Assert.Contains("admin", connStr);
        Assert.Contains("secret123", connStr);
    }

    [Fact]
    public void BuildConnectionString_SqlServer_WindowsAuth_NoCredentials()
    {
        var info = new ConnectionInfo
        {
            Server = "localhost",
            Database = "TestDb",
            Port = 1433,
            UseSqlAuthentication = false,
            Platform = DatabasePlatform.SqlServer
        };

        var connStr = info.BuildConnectionString();
        Assert.Contains("Integrated Security=True", connStr);
        Assert.DoesNotContain("User ID", connStr);
    }

    [Fact]
    public void BuildConnectionString_SqlServer_NonDefaultPort_IncludesPort()
    {
        var info = new ConnectionInfo
        {
            Server = "localhost",
            Database = "TestDb",
            Port = 1444,
            UseSqlAuthentication = true,
            Username = "sa",
            Password = "pass",
            Platform = DatabasePlatform.SqlServer
        };

        var connStr = info.BuildConnectionString();
        Assert.Contains("localhost,1444", connStr);
    }

    // -- PostgreSQL connection strings --

    [Fact]
    public void BuildConnectionString_PostgreSql_BasicFormat()
    {
        var info = new ConnectionInfo
        {
            Server = "pghost",
            Database = "PgDb",
            Port = 5432,
            Platform = DatabasePlatform.PostgreSql
        };

        var connStr = info.BuildConnectionString();

        Assert.Contains("Host=pghost", connStr);
        Assert.Contains("Port=5432", connStr);
        Assert.Contains("Database=PgDb", connStr);
        Assert.DoesNotContain("Username=", connStr);
        Assert.DoesNotContain("Password=", connStr);
    }

    [Fact]
    public void BuildConnectionString_PostgreSql_WithCredentials()
    {
        var info = new ConnectionInfo
        {
            Server = "pghost",
            Database = "PgDb",
            Port = 5432,
            Username = "pgadmin",
            Password = "pgpass",
            Platform = DatabasePlatform.PostgreSql
        };

        var connStr = info.BuildConnectionString();

        Assert.Contains("Host=pghost", connStr);
        Assert.Contains("Port=5432", connStr);
        Assert.Contains("Database=PgDb", connStr);
        Assert.Contains("Username=pgadmin", connStr);
        Assert.Contains("Password=pgpass", connStr);
    }

    [Fact]
    public void BuildConnectionString_PostgreSql_TrustCertificate()
    {
        var info = new ConnectionInfo
        {
            Server = "pghost",
            Database = "PgDb",
            Port = 5432,
            TrustServerCertificate = true,
            Platform = DatabasePlatform.PostgreSql
        };

        var connStr = info.BuildConnectionString();
        Assert.Contains("Trust Server Certificate=true", connStr);
    }

    [Fact]
    public void BuildConnectionString_PostgreSql_CustomPort()
    {
        var info = new ConnectionInfo
        {
            Server = "pghost",
            Database = "PgDb",
            Port = 5433,
            Platform = DatabasePlatform.PostgreSql
        };

        var connStr = info.BuildConnectionString();
        Assert.Contains("Port=5433", connStr);
    }
}