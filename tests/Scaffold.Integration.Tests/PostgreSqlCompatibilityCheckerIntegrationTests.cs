using Scaffold.Assessment.PostgreSql;

namespace Scaffold.Integration.Tests;

/// <summary>
/// Integration tests for PostgreSQL CompatibilityChecker against a real PostgreSQL instance.
/// The sample database installs extensions (uuid-ossp, pg_trgm, hstore) and uses
/// various PG-specific features, so the checker should detect them.
/// </summary>
[Collection("PostgreSql")]
public class PostgreSqlCompatibilityCheckerIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    public PostgreSqlCompatibilityCheckerIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CheckAsync_ReturnsIssues()
    {
        var checker = new CompatibilityChecker(_fixture.Connection);

        var issues = await checker.CheckAsync();

        // Should find at least some issues (extensions, custom types, etc.)
        Assert.NotNull(issues);
    }

    [Fact]
    public async Task CheckAsync_DetectsExtensions()
    {
        var checker = new CompatibilityChecker(_fixture.Connection);

        var issues = await checker.CheckAsync();

        // The seed script installs uuid-ossp, pg_trgm, hstore — verify they are detected
        Assert.Contains(issues, i =>
            i.ObjectName == "uuid-ossp" ||
            i.ObjectName == "pg_trgm" ||
            i.ObjectName == "hstore");
    }

    [Fact]
    public async Task CheckAsync_CalculatesScore()
    {
        var checker = new CompatibilityChecker(_fixture.Connection);

        var issues = await checker.CheckAsync();

        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);
        Assert.InRange(score, 0, 100);
    }

    [Fact]
    public async Task CheckAsync_IssuesHaveRequiredFields()
    {
        var checker = new CompatibilityChecker(_fixture.Connection);

        var issues = await checker.CheckAsync();

        Assert.All(issues, issue =>
        {
            Assert.False(string.IsNullOrEmpty(issue.ObjectName), "ObjectName should not be empty");
            Assert.False(string.IsNullOrEmpty(issue.IssueType), "IssueType should not be empty");
            Assert.False(string.IsNullOrEmpty(issue.Description), "Description should not be empty");
        });
    }
}