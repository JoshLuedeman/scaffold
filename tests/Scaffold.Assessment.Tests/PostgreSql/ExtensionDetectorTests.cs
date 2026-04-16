using Scaffold.Assessment.PostgreSql;

namespace Scaffold.Assessment.Tests.PostgreSql;

public class ExtensionDetectorTests
{
    // -- Known supported extensions -----------------------------------

    [Theory]
    [InlineData("postgis")]
    [InlineData("pg_trgm")]
    [InlineData("hstore")]
    [InlineData("uuid-ossp")]
    [InlineData("pgcrypto")]
    [InlineData("pg_cron")]
    [InlineData("pgaudit")]
    [InlineData("pg_stat_statements")]
    [InlineData("vector")]
    [InlineData("timescaledb")]
    public void Evaluate_KnownSupportedExtension_ReturnsCompatible(string extensionName)
    {
        var result = ExtensionDetector.Evaluate(extensionName);

        Assert.True(result.IsSupported);
        Assert.Equal("Compatible", result.Status);
        Assert.Equal(extensionName, result.ExtensionName);
        Assert.Null(result.Recommendation);
    }

    // -- Unknown extensions return Incompatible -----------------------

    [Theory]
    [InlineData("my_custom_ext")]
    [InlineData("totally_unknown")]
    [InlineData("proprietary_extension")]
    public void Evaluate_UnknownExtension_ReturnsIncompatible(string extensionName)
    {
        var result = ExtensionDetector.Evaluate(extensionName);

        Assert.False(result.IsSupported);
        Assert.Equal("Incompatible", result.Status);
        Assert.Equal(extensionName, result.ExtensionName);
        Assert.NotNull(result.Recommendation);
        Assert.Contains(extensionName, result.Recommendation);
        Assert.Contains("Azure Database for PostgreSQL - Flexible Server", result.Recommendation);
    }

    // -- Case insensitivity ------------------------------------------

    [Theory]
    [InlineData("POSTGIS")]
    [InlineData("PostGIS")]
    [InlineData("Postgis")]
    [InlineData("PG_TRGM")]
    [InlineData("Hstore")]
    [InlineData("UUID-OSSP")]
    public void Evaluate_IsCaseInsensitive(string extensionName)
    {
        var result = ExtensionDetector.Evaluate(extensionName);

        Assert.True(result.IsSupported);
        Assert.Equal("Compatible", result.Status);
    }

    // -- EvaluateAll with mixed results -------------------------------

    [Fact]
    public void EvaluateAll_MixedExtensions_ReturnsCorrectResults()
    {
        var extensions = new[] { "postgis", "my_custom_ext", "pg_cron", "unknown_ext" };

        var results = ExtensionDetector.EvaluateAll(extensions);

        Assert.Equal(4, results.Count);

        Assert.True(results[0].IsSupported);
        Assert.Equal("Compatible", results[0].Status);

        Assert.False(results[1].IsSupported);
        Assert.Equal("Incompatible", results[1].Status);

        Assert.True(results[2].IsSupported);
        Assert.Equal("Compatible", results[2].Status);

        Assert.False(results[3].IsSupported);
        Assert.Equal("Incompatible", results[3].Status);
    }

    [Fact]
    public void EvaluateAll_AllSupported_ReturnsAllCompatible()
    {
        var extensions = new[] { "postgis", "hstore", "pgcrypto", "pg_trgm" };

        var results = ExtensionDetector.EvaluateAll(extensions);

        Assert.All(results, r =>
        {
            Assert.True(r.IsSupported);
            Assert.Equal("Compatible", r.Status);
            Assert.Null(r.Recommendation);
        });
    }

    [Fact]
    public void EvaluateAll_AllUnsupported_ReturnsAllIncompatible()
    {
        var extensions = new[] { "custom_a", "custom_b", "custom_c" };

        var results = ExtensionDetector.EvaluateAll(extensions);

        Assert.All(results, r =>
        {
            Assert.False(r.IsSupported);
            Assert.Equal("Incompatible", r.Status);
            Assert.NotNull(r.Recommendation);
        });
    }

    // -- Empty list returns empty results -----------------------------

    [Fact]
    public void EvaluateAll_EmptyList_ReturnsEmptyResults()
    {
        var results = ExtensionDetector.EvaluateAll(Array.Empty<string>());

        Assert.Empty(results);
    }

    // -- Result properties are populated correctly --------------------

    [Fact]
    public void Evaluate_SupportedExtension_HasCorrectProperties()
    {
        var result = ExtensionDetector.Evaluate("postgis");

        Assert.Equal("postgis", result.ExtensionName);
        Assert.True(result.IsSupported);
        Assert.Equal("Compatible", result.Status);
        Assert.Null(result.Recommendation);
    }

    [Fact]
    public void Evaluate_UnsupportedExtension_RecommendationSuggestsVm()
    {
        var result = ExtensionDetector.Evaluate("my_custom_ext");

        Assert.NotNull(result.Recommendation);
        Assert.Contains("PostgreSQL on Azure VM", result.Recommendation);
    }
}
