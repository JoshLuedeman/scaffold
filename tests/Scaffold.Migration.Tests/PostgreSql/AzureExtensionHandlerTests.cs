using Scaffold.Migration.PostgreSql;
using Scaffold.Migration.PostgreSql.Models;

namespace Scaffold.Migration.Tests.PostgreSql;

public class AzureExtensionHandlerTests
{
    private readonly AzureExtensionHandler _handler = new();

    #region EvaluateExtensions — All Supported

    [Fact]
    public void EvaluateExtensions_AllSupported_AllInToInstall()
    {
        var source = new List<string> { "pgcrypto", "uuid-ossp", "citext" };

        var plan = _handler.EvaluateExtensions(source);

        Assert.Equal(3, plan.ToInstall.Count);
        Assert.Empty(plan.Skipped);
        Assert.Contains("pgcrypto", plan.ToInstall);
        Assert.Contains("uuid-ossp", plan.ToInstall);
        Assert.Contains("citext", plan.ToInstall);
    }

    #endregion

    #region EvaluateExtensions — Unsupported

    [Fact]
    public void EvaluateExtensions_UnsupportedExtension_InSkippedWithWarning()
    {
        var source = new List<string> { "pgcrypto", "unsupported_ext" };

        var plan = _handler.EvaluateExtensions(source);

        Assert.Single(plan.Skipped);
        Assert.Contains("unsupported_ext", plan.Skipped);
        Assert.Contains("pgcrypto", plan.ToInstall);

        var warning = plan.Warnings.First(w => w.ExtensionName == "unsupported_ext");
        Assert.Equal(ExtensionWarningSeverity.Warning, warning.Severity);
        Assert.Contains("not supported", warning.Message);
    }

    #endregion

    #region EvaluateExtensions — SharedPreload

    [Fact]
    public void EvaluateExtensions_RequiresSharedPreload_InToInstallWithInfoWarning()
    {
        var source = new List<string> { "pg_stat_statements" };

        var plan = _handler.EvaluateExtensions(source);

        Assert.Single(plan.ToInstall);
        Assert.Contains("pg_stat_statements", plan.ToInstall);
        Assert.Empty(plan.Skipped);

        var warning = plan.Warnings.First(w => w.ExtensionName == "pg_stat_statements");
        Assert.Equal(ExtensionWarningSeverity.Info, warning.Severity);
        Assert.Contains("shared_preload_libraries", warning.Message);
    }

    [Theory]
    [InlineData("pg_cron")]
    [InlineData("pg_hint_plan")]
    [InlineData("pgaudit")]
    [InlineData("pg_partman")]
    [InlineData("timescaledb")]
    [InlineData("pglogical")]
    public void EvaluateExtensions_VariousPreloadExtensions_AllGetInfoWarning(string ext)
    {
        var plan = _handler.EvaluateExtensions([ext]);

        Assert.Single(plan.ToInstall);
        Assert.Single(plan.Warnings);
        Assert.Equal(ExtensionWarningSeverity.Info, plan.Warnings[0].Severity);
    }

    #endregion

    #region EvaluateExtensions — Dependency Ordering

    [Fact]
    public void EvaluateExtensions_PostgisTopology_PostgisFirst()
    {
        var source = new List<string> { "postgis_topology", "postgis" };

        var plan = _handler.EvaluateExtensions(source);

        Assert.Equal(2, plan.ToInstall.Count);
        var postgisIdx = plan.ToInstall.IndexOf("postgis");
        var topologyIdx = plan.ToInstall.IndexOf("postgis_topology");
        Assert.True(postgisIdx < topologyIdx,
            $"postgis (at {postgisIdx}) should come before postgis_topology (at {topologyIdx})");
    }

    [Fact]
    public void EvaluateExtensions_PostgisTigerGeocoder_DependenciesFirst()
    {
        var source = new List<string> { "postgis_tiger_geocoder", "fuzzystrmatch", "postgis" };

        var plan = _handler.EvaluateExtensions(source);

        Assert.Equal(3, plan.ToInstall.Count);
        var postgisIdx = plan.ToInstall.IndexOf("postgis");
        var fuzzyIdx = plan.ToInstall.IndexOf("fuzzystrmatch");
        var geocoderIdx = plan.ToInstall.IndexOf("postgis_tiger_geocoder");

        Assert.True(postgisIdx < geocoderIdx,
            "postgis should come before postgis_tiger_geocoder");
        Assert.True(fuzzyIdx < geocoderIdx,
            "fuzzystrmatch should come before postgis_tiger_geocoder");
    }

    [Fact]
    public void EvaluateExtensions_EarthdistanceDependsOnCube_CubeFirst()
    {
        var source = new List<string> { "earthdistance", "cube" };

        var plan = _handler.EvaluateExtensions(source);

        Assert.Equal(2, plan.ToInstall.Count);
        var cubeIdx = plan.ToInstall.IndexOf("cube");
        var earthIdx = plan.ToInstall.IndexOf("earthdistance");
        Assert.True(cubeIdx < earthIdx, "cube should come before earthdistance");
    }

    [Fact]
    public void EvaluateExtensions_DependencyNotInSource_DependentStillIncluded()
    {
        // postgis_topology depends on postgis, but postgis not in source
        var source = new List<string> { "postgis_topology" };

        var plan = _handler.EvaluateExtensions(source);

        Assert.Single(plan.ToInstall);
        Assert.Contains("postgis_topology", plan.ToInstall);
    }

    [Fact]
    public void EvaluateExtensions_PgroutingDependsOnPostgis_PostgisFirst()
    {
        var source = new List<string> { "pgrouting", "postgis" };

        var plan = _handler.EvaluateExtensions(source);

        var postgisIdx = plan.ToInstall.IndexOf("postgis");
        var routingIdx = plan.ToInstall.IndexOf("pgrouting");
        Assert.True(postgisIdx < routingIdx, "postgis should come before pgrouting");
    }

    #endregion

    #region EvaluateExtensions — Mixed

    [Fact]
    public void EvaluateExtensions_MixedSupportedAndUnsupported_CorrectlySeparates()
    {
        var source = new List<string> { "pgcrypto", "unsupported_one", "citext", "unsupported_two" };

        var plan = _handler.EvaluateExtensions(source);

        Assert.Equal(2, plan.ToInstall.Count);
        Assert.Equal(2, plan.Skipped.Count);
        Assert.Contains("pgcrypto", plan.ToInstall);
        Assert.Contains("citext", plan.ToInstall);
        Assert.Contains("unsupported_one", plan.Skipped);
        Assert.Contains("unsupported_two", plan.Skipped);
    }

    #endregion

    #region EvaluateExtensions — Empty

    [Fact]
    public void EvaluateExtensions_EmptyList_ReturnsEmptyPlan()
    {
        var plan = _handler.EvaluateExtensions([]);

        Assert.Empty(plan.ToInstall);
        Assert.Empty(plan.Skipped);
        Assert.Empty(plan.Warnings);
    }

    #endregion

    #region OrderByDependencies

    [Fact]
    public void OrderByDependencies_NoDependencies_PreservesOrder()
    {
        var extensions = new List<string> { "pgcrypto", "citext", "hstore" };

        var ordered = AzureExtensionHandler.OrderByDependencies(extensions);

        Assert.Equal(3, ordered.Count);
        Assert.Equal("pgcrypto", ordered[0]);
        Assert.Equal("citext", ordered[1]);
        Assert.Equal("hstore", ordered[2]);
    }

    [Fact]
    public void OrderByDependencies_WithDependencyChain_DepsFirst()
    {
        var extensions = new List<string>
        {
            "postgis_raster", "postgis_topology", "postgis"
        };

        var ordered = AzureExtensionHandler.OrderByDependencies(extensions);

        var postgisIdx = ordered.IndexOf("postgis");
        var rasterIdx = ordered.IndexOf("postgis_raster");
        var topoIdx = ordered.IndexOf("postgis_topology");

        Assert.True(postgisIdx < rasterIdx, "postgis should come before postgis_raster");
        Assert.True(postgisIdx < topoIdx, "postgis should come before postgis_topology");
    }

    [Fact]
    public void OrderByDependencies_AddressStandard_DependencyFirst()
    {
        var extensions = new List<string> { "address_standardizer_data_us", "address_standardizer" };

        var ordered = AzureExtensionHandler.OrderByDependencies(extensions);

        Assert.Equal("address_standardizer", ordered[0]);
        Assert.Equal("address_standardizer_data_us", ordered[1]);
    }

    [Fact]
    public void OrderByDependencies_SingleExtension_ReturnsSame()
    {
        var extensions = new List<string> { "pgcrypto" };

        var ordered = AzureExtensionHandler.OrderByDependencies(extensions);

        Assert.Single(ordered);
        Assert.Equal("pgcrypto", ordered[0]);
    }

    [Fact]
    public void OrderByDependencies_Empty_ReturnsEmpty()
    {
        var ordered = AzureExtensionHandler.OrderByDependencies([]);
        Assert.Empty(ordered);
    }

    [Fact]
    public void OrderByDependencies_ComplexPostgisChain_CorrectOrder()
    {
        // postgis_tiger_geocoder depends on postgis + fuzzystrmatch
        // pgrouting depends on postgis
        var extensions = new List<string>
        {
            "postgis_tiger_geocoder", "pgrouting", "fuzzystrmatch", "postgis"
        };

        var ordered = AzureExtensionHandler.OrderByDependencies(extensions);

        var postgisIdx = ordered.IndexOf("postgis");
        var fuzzyIdx = ordered.IndexOf("fuzzystrmatch");
        var geocoderIdx = ordered.IndexOf("postgis_tiger_geocoder");
        var routingIdx = ordered.IndexOf("pgrouting");

        Assert.True(postgisIdx < geocoderIdx, "postgis before postgis_tiger_geocoder");
        Assert.True(fuzzyIdx < geocoderIdx, "fuzzystrmatch before postgis_tiger_geocoder");
        Assert.True(postgisIdx < routingIdx, "postgis before pgrouting");
    }

    #endregion

    #region ExtensionMigrationResult

    [Fact]
    public void ExtensionMigrationResult_NoFailures_SuccessIsTrue()
    {
        var result = new ExtensionMigrationResult
        {
            Installed = ["pgcrypto", "citext"],
            Skipped = ["unsupported"],
            Failed = []
        };

        Assert.True(result.Success);
    }

    [Fact]
    public void ExtensionMigrationResult_WithFailures_SuccessIsFalse()
    {
        var result = new ExtensionMigrationResult
        {
            Installed = ["pgcrypto"],
            Failed = ["citext"]
        };

        Assert.False(result.Success);
    }

    #endregion

    #region EvaluateExtensions — Full Azure Supported List Spot Checks

    [Theory]
    [InlineData("pgcrypto")]
    [InlineData("uuid-ossp")]
    [InlineData("postgis")]
    [InlineData("vector")]
    [InlineData("pg_trgm")]
    [InlineData("hstore")]
    [InlineData("ltree")]
    [InlineData("citext")]
    [InlineData("btree_gin")]
    [InlineData("postgres_fdw")]
    public void EvaluateExtensions_KnownSupportedExtension_InToInstall(string ext)
    {
        var plan = _handler.EvaluateExtensions([ext]);

        Assert.Single(plan.ToInstall);
        Assert.Contains(ext, plan.ToInstall);
        Assert.Empty(plan.Skipped);
    }

    [Theory]
    [InlineData("pg_repack")]
    [InlineData("tablefunc")]
    [InlineData("unaccent")]
    [InlineData("intarray")]
    [InlineData("semver")]
    public void EvaluateExtensions_MoreSupportedExtensions_InToInstall(string ext)
    {
        var plan = _handler.EvaluateExtensions([ext]);

        Assert.Single(plan.ToInstall);
        Assert.Empty(plan.Skipped);
    }

    #endregion
}
