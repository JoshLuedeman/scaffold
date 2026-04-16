using Scaffold.Core.Enums;

namespace Scaffold.Assessment.Tests;

public class DatabasePlatformTests
{
    [Fact]
    public void DatabasePlatform_HasExpectedMembers()
    {
        var names = Enum.GetNames<DatabasePlatform>();

        Assert.Contains("SqlServer", names);
        Assert.Contains("PostgreSql", names);
        Assert.Equal(2, names.Length);
    }

    [Fact]
    public void DatabasePlatform_SqlServer_IsDefault()
    {
        var defaultValue = default(DatabasePlatform);

        Assert.Equal(DatabasePlatform.SqlServer, defaultValue);
        Assert.Equal(0, (int)defaultValue);
    }

    [Theory]
    [InlineData(DatabasePlatform.SqlServer, 0)]
    [InlineData(DatabasePlatform.PostgreSql, 1)]
    public void DatabasePlatform_HasExpectedIntegerValues(DatabasePlatform platform, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)platform);
    }

    [Theory]
    [InlineData(DatabasePlatform.SqlServer, "SqlServer")]
    [InlineData(DatabasePlatform.PostgreSql, "PostgreSql")]
    public void DatabasePlatform_RoundTrips_ThroughStringConversion(DatabasePlatform platform, string expectedName)
    {
        // Convert to string
        var asString = platform.ToString();
        Assert.Equal(expectedName, asString);

        // Parse back from string
        var parsed = Enum.Parse<DatabasePlatform>(asString);
        Assert.Equal(platform, parsed);
    }
}
