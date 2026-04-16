using Microsoft.AspNetCore.DataProtection;
using Scaffold.Core.Interfaces;
using Scaffold.Infrastructure.Security;

namespace Scaffold.Api.Tests;

public class ConnectionStringProtectorTests
{
    private readonly IConnectionStringProtector _sut;

    public ConnectionStringProtectorTests()
    {
        var provider = DataProtectionProvider.Create("Scaffold.Tests");
        _sut = new ConnectionStringProtector(provider);
    }

    [Fact]
    public void Protect_ThenUnprotect_ReturnsOriginalValue()
    {
        var original = "Server=myserver.database.windows.net;Database=MyDb;User Id=sa;Password=SuperSecret123!;";

        var protectedValue = _sut.Protect(original);
        var unprotectedValue = _sut.Unprotect(protectedValue);

        Assert.Equal(original, unprotectedValue);
    }

    [Fact]
    public void Protect_ReturnsDifferentValueFromOriginal()
    {
        var original = "Server=myserver;Database=mydb;User Id=admin;Password=pass;";

        var protectedValue = _sut.Protect(original);

        Assert.NotEqual(original, protectedValue);
    }

    [Fact]
    public void Protect_SameInputTwice_ProducesDifferentCiphertext()
    {
        var original = "Server=myserver;Database=mydb;";

        var first = _sut.Protect(original);
        var second = _sut.Protect(original);

        // Data Protection uses unique nonces, so ciphertext differs each time
        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("Server=a;")]
    [InlineData("Server=longhost.database.windows.net;Database=LargeDb;User Id=admin;Password=Str0ng!P@ss;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;")]
    public void RoundTrip_VariousConnectionStrings_ReturnsOriginal(string connectionString)
    {
        var protectedValue = _sut.Protect(connectionString);
        var result = _sut.Unprotect(protectedValue);

        Assert.Equal(connectionString, result);
    }

    [Fact]
    public void Protect_NullInput_ThrowsException()
    {
        Assert.ThrowsAny<Exception>(() => _sut.Protect(null!));
    }

    [Fact]
    public void Unprotect_NullInput_ThrowsException()
    {
        Assert.ThrowsAny<Exception>(() => _sut.Unprotect(null!));
    }

    [Fact]
    public void Unprotect_InvalidCiphertext_ThrowsException()
    {
        Assert.ThrowsAny<Exception>(() => _sut.Unprotect("not-a-valid-protected-payload"));
    }
}