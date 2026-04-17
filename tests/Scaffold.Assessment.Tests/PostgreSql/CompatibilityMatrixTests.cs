using Scaffold.Assessment.PostgreSql;
using Scaffold.Core.Enums;

namespace Scaffold.Assessment.Tests.PostgreSql;

public class CompatibilityMatrixTests
{
    private const string FlexibleServer = "Azure Database for PostgreSQL - Flexible Server";
    private const string PgOnVm = "PostgreSQL on Azure VM";

    // ── GetSeverity returns correct values for known issue types ─────

    [Theory]
    [InlineData("Superuser Access", CompatibilitySeverity.Unsupported)]
    [InlineData("Custom C Extensions", CompatibilitySeverity.Unsupported)]
    [InlineData("Foreign Data Wrappers", CompatibilitySeverity.Partial)]
    [InlineData("Tablespace (Custom)", CompatibilitySeverity.Unsupported)]
    [InlineData("Unsupported Extension", CompatibilitySeverity.Unsupported)]
    [InlineData("Database Size > 16TB", CompatibilitySeverity.Unsupported)]
    [InlineData("PG Version < 12", CompatibilitySeverity.Unsupported)]
    [InlineData("PL/Python", CompatibilitySeverity.Unsupported)]
    [InlineData("PL/Perl", CompatibilitySeverity.Unsupported)]
    [InlineData("PL/Tcl", CompatibilitySeverity.Unsupported)]
    [InlineData("Custom Collation", CompatibilitySeverity.Partial)]
    [InlineData("Deprecated Data Types (money precision)", CompatibilitySeverity.Partial)]
    public void GetSeverity_FlexibleServer_ReturnsCorrectSeverity(string issueType, CompatibilitySeverity expected)
    {
        var severity = CompatibilityMatrix.GetSeverity(issueType, FlexibleServer);

        Assert.Equal(expected, severity);
    }

    [Theory]
    [InlineData("Superuser Access")]
    [InlineData("Custom C Extensions")]
    [InlineData("Foreign Data Wrappers")]
    [InlineData("PL/Python")]
    [InlineData("Unsupported Extension")]
    [InlineData("PostGIS")]
    [InlineData("pg_cron")]
    [InlineData("JSONB")]
    public void GetSeverity_PgOnVm_ReturnsSupported(string issueType)
    {
        var severity = CompatibilityMatrix.GetSeverity(issueType, PgOnVm);

        Assert.Equal(CompatibilitySeverity.Supported, severity);
    }

    [Theory]
    [InlineData("PostGIS")]
    [InlineData("pg_cron")]
    [InlineData("pgaudit")]
    [InlineData("pg_trgm")]
    [InlineData("hstore")]
    [InlineData("uuid-ossp")]
    [InlineData("pgcrypto")]
    [InlineData("pg_stat_statements")]
    [InlineData("Event Triggers")]
    [InlineData("Logical Replication")]
    [InlineData("Partitioning")]
    [InlineData("Multiple Databases Per Instance")]
    [InlineData("Connection Pooling (PgBouncer)")]
    [InlineData("Full Text Search")]
    [InlineData("JSONB")]
    [InlineData("Array Types")]
    [InlineData("Range Types")]
    [InlineData("Composite Types")]
    [InlineData("Domain Types")]
    public void GetSeverity_SupportedFeatures_ReturnsSupportedOnBothTargets(string issueType)
    {
        Assert.Equal(CompatibilitySeverity.Supported, CompatibilityMatrix.GetSeverity(issueType, FlexibleServer));
        Assert.Equal(CompatibilitySeverity.Supported, CompatibilityMatrix.GetSeverity(issueType, PgOnVm));
    }

    // ── GetSeverity returns Supported for unknown issue types ────────

    [Fact]
    public void GetSeverity_UnknownIssueType_ReturnsSupportedByDefault()
    {
        var severity = CompatibilityMatrix.GetSeverity("NonExistentFeature", FlexibleServer);

        Assert.Equal(CompatibilitySeverity.Supported, severity);
    }

    [Fact]
    public void GetSeverity_UnknownTargetService_ReturnsSupportedByDefault()
    {
        var severity = CompatibilityMatrix.GetSeverity("Superuser Access", "Unknown Service");

        Assert.Equal(CompatibilitySeverity.Supported, severity);
    }

    // ── GetDocUrl returns URL for unsupported features ───────────────

    [Theory]
    [InlineData("Superuser Access")]
    [InlineData("Custom C Extensions")]
    [InlineData("Unsupported Extension")]
    [InlineData("Database Size > 16TB")]
    [InlineData("PG Version < 12")]
    [InlineData("PL/Python")]
    public void GetDocUrl_UnsupportedOnFlexibleServer_ReturnsUrl(string issueType)
    {
        var docUrl = CompatibilityMatrix.GetDocUrl(issueType, FlexibleServer);

        Assert.NotNull(docUrl);
        Assert.StartsWith("https://", docUrl);
    }

    [Theory]
    [InlineData("PostGIS")]
    [InlineData("pg_cron")]
    [InlineData("JSONB")]
    [InlineData("Event Triggers")]
    public void GetDocUrl_SupportedFeature_ReturnsNull(string issueType)
    {
        var docUrl = CompatibilityMatrix.GetDocUrl(issueType, FlexibleServer);

        Assert.Null(docUrl);
    }

    [Fact]
    public void GetDocUrl_UnknownIssueType_ReturnsNull()
    {
        var docUrl = CompatibilityMatrix.GetDocUrl("NonExistentFeature", FlexibleServer);

        Assert.Null(docUrl);
    }

    // ── All target service names work ───────────────────────────────

    [Theory]
    [InlineData("Azure Database for PostgreSQL - Flexible Server")]
    [InlineData("PostgreSQL on Azure VM")]
    public void GetSeverity_AllTargetServiceNames_ReturnValidSeverity(string targetService)
    {
        var severity = CompatibilityMatrix.GetSeverity("Superuser Access", targetService);

        Assert.True(Enum.IsDefined(severity));
    }

    [Theory]
    [InlineData("Azure Database for PostgreSQL - Flexible Server")]
    [InlineData("PostgreSQL on Azure VM")]
    public void GetDocUrl_AllTargetServiceNames_DoNotThrow(string targetService)
    {
        // Should not throw for any valid target service
        var docUrl = CompatibilityMatrix.GetDocUrl("Superuser Access", targetService);

        // FlexibleServer should have a URL (unsupported), VM should not (supported)
        if (targetService == FlexibleServer)
            Assert.NotNull(docUrl);
        else
            Assert.Null(docUrl);
    }

    // ── Case-insensitivity ──────────────────────────────────────────

    [Theory]
    [InlineData("superuser access")]
    [InlineData("SUPERUSER ACCESS")]
    [InlineData("Superuser Access")]
    [InlineData("sUpErUsEr AcCeSs")]
    public void GetSeverity_IsCaseInsensitive_ForIssueType(string issueType)
    {
        var severity = CompatibilityMatrix.GetSeverity(issueType, FlexibleServer);

        Assert.Equal(CompatibilitySeverity.Unsupported, severity);
    }

    [Theory]
    [InlineData("azure database for postgresql - flexible server")]
    [InlineData("AZURE DATABASE FOR POSTGRESQL - FLEXIBLE SERVER")]
    [InlineData("Azure Database for PostgreSQL - Flexible Server")]
    public void GetSeverity_IsCaseInsensitive_ForTargetService(string targetService)
    {
        var severity = CompatibilityMatrix.GetSeverity("Superuser Access", targetService);

        Assert.Equal(CompatibilitySeverity.Unsupported, severity);
    }

    [Theory]
    [InlineData("foreign data wrappers")]
    [InlineData("FOREIGN DATA WRAPPERS")]
    public void GetDocUrl_IsCaseInsensitive(string issueType)
    {
        var docUrl = CompatibilityMatrix.GetDocUrl(issueType, FlexibleServer);

        Assert.NotNull(docUrl);
    }
}
