using Npgsql;
using Scaffold.Assessment.PostgreSql;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests.PostgreSql;

public class PostgreSqlConnectionFactoryTests
{
    private readonly PostgreSqlConnectionFactory _factory = new();

    // ── Connection string builder basics ──────────────────────────────

    [Fact]
    public async Task BuildConnectionStringBuilderAsync_SetsHostFromServer()
    {
        var info = CreateConnectionInfo(server: "myhost.example.com");

        var builder = await _factory.BuildConnectionStringBuilderAsync(info);

        Assert.Equal("myhost.example.com", builder.Host);
    }

    [Fact]
    public async Task BuildConnectionStringBuilderAsync_SetsDatabaseFromInfo()
    {
        var info = CreateConnectionInfo(database: "test_db");

        var builder = await _factory.BuildConnectionStringBuilderAsync(info);

        Assert.Equal("test_db", builder.Database);
    }

    [Fact]
    public async Task BuildConnectionStringBuilderAsync_SetsUsernameAndPassword()
    {
        var info = CreateConnectionInfo(username: "admin", password: "secret123");

        var builder = await _factory.BuildConnectionStringBuilderAsync(info);

        Assert.Equal("admin", builder.Username);
        Assert.Equal("secret123", builder.Password);
    }

    // ── Port defaulting ─────────────────────────────────────────────

    [Fact]
    public async Task BuildConnectionStringBuilderAsync_UsesPgDefaultPort_WhenSqlServerDefault()
    {
        var info = CreateConnectionInfo(port: 1433);

        var builder = await _factory.BuildConnectionStringBuilderAsync(info);

        Assert.Equal(5432, builder.Port);
    }

    [Theory]
    [InlineData(5432)]
    [InlineData(5433)]
    [InlineData(15432)]
    public async Task BuildConnectionStringBuilderAsync_PreservesCustomPort(int port)
    {
        var info = CreateConnectionInfo(port: port);

        var builder = await _factory.BuildConnectionStringBuilderAsync(info);

        Assert.Equal(port, builder.Port);
    }

    // ── SSL mode ────────────────────────────────────────────────────

    [Fact]
    public async Task BuildConnectionStringBuilderAsync_SetsSslModePrefer_WhenTrustCertTrue()
    {
        var info = CreateConnectionInfo(trustServerCertificate: true);

        var builder = await _factory.BuildConnectionStringBuilderAsync(info);

        Assert.Equal(SslMode.Prefer, builder.SslMode);
    }

    [Fact]
    public async Task BuildConnectionStringBuilderAsync_SetsSslModeRequire_WhenTrustCertFalse()
    {
        var info = CreateConnectionInfo(trustServerCertificate: false);

        var builder = await _factory.BuildConnectionStringBuilderAsync(info);

        Assert.Equal(SslMode.Require, builder.SslMode);
    }

    // ── Password resolution ─────────────────────────────────────────

    [Fact]
    public async Task BuildConnectionStringBuilderAsync_UsesDirectPassword_WhenProvided()
    {
        var info = CreateConnectionInfo(username: "user", password: "directpw");

        var builder = await _factory.BuildConnectionStringBuilderAsync(info);

        Assert.Equal("directpw", builder.Password);
    }

    [Fact]
    public async Task BuildConnectionStringBuilderAsync_ThrowsInvalidOperation_WhenNoPasswordOrKeyVaultUri()
    {
        var info = CreateConnectionInfo(username: "user", password: null);
        info.KeyVaultSecretUri = null;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _factory.BuildConnectionStringBuilderAsync(info));

        Assert.Contains("Password", ex.Message);
        Assert.Contains("KeyVaultSecretUri", ex.Message);
    }

    // ── No-credentials scenario (integrated auth) ───────────────────

    [Fact]
    public async Task BuildConnectionStringBuilderAsync_SetsNoCredentials_WhenUsernameIsEmpty()
    {
        var info = CreateConnectionInfo(username: null, password: null);

        var builder = await _factory.BuildConnectionStringBuilderAsync(info);

        Assert.True(string.IsNullOrEmpty(builder.Username));
        Assert.True(string.IsNullOrEmpty(builder.Password));
    }

    // ── Full connection string composition ───────────────────────────

    [Fact]
    public async Task BuildConnectionStringBuilderAsync_ProducesValidConnectionString()
    {
        var info = CreateConnectionInfo(
            server: "pg.local",
            port: 5432,
            database: "mydb",
            username: "appuser",
            password: "apppw",
            trustServerCertificate: true);

        var builder = await _factory.BuildConnectionStringBuilderAsync(info);
        var connString = builder.ConnectionString;

        Assert.Contains("Host=pg.local", connString);
        Assert.Contains("Port=5432", connString);
        Assert.Contains("Database=mydb", connString);
        Assert.Contains("Username=appuser", connString);
        Assert.Contains("Password=apppw", connString);
        Assert.Contains("SSL Mode=Prefer", connString);
    }

    [Theory]
    [InlineData(1433, 5432)]
    [InlineData(5432, 5432)]
    [InlineData(5433, 5433)]
    public async Task BuildConnectionStringBuilderAsync_PortMapping_Theory(int inputPort, int expectedPort)
    {
        var info = CreateConnectionInfo(port: inputPort);

        var builder = await _factory.BuildConnectionStringBuilderAsync(info);

        Assert.Equal(expectedPort, builder.Port);
    }

    // ── Helper ──────────────────────────────────────────────────────

    private static ConnectionInfo CreateConnectionInfo(
        string server = "localhost",
        int port = 5432,
        string database = "testdb",
        string? username = null,
        string? password = null,
        bool trustServerCertificate = true)
    {
        return new ConnectionInfo
        {
            Server = server,
            Port = port,
            Database = database,
            Username = username,
            Password = password,
            TrustServerCertificate = trustServerCertificate
        };
    }
}
