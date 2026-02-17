using Scaffold.Assessment.SqlServer;

namespace Scaffold.Integration.Tests;

/// <summary>
/// Integration tests for CompatibilityChecker against a real SQL Server instance.
/// The sample database has standard features (views, procs, triggers) but no
/// exotic features like CLR assemblies or linked servers, so the checker should
/// find few or no blocking issues.
/// </summary>
[Collection("SqlServer")]
public class CompatibilityCheckerIntegrationTests
{
    private readonly SqlServerFixture _fixture;

    public CompatibilityCheckerIntegrationTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CheckAsync_ReturnsIssuesList()
    {
        var checker = new CompatibilityChecker(_fixture.Connection);

        var issues = await checker.CheckAsync();

        Assert.NotNull(issues);
        // The sample database is simple — should have few or no blocking issues
    }

    [Fact]
    public async Task CheckAsync_NoClrAssemblies_InSampleDb()
    {
        var checker = new CompatibilityChecker(_fixture.Connection);

        var issues = await checker.CheckAsync();

        Assert.DoesNotContain(issues, i => i.IssueType == "CLR Assembly");
    }

    [Fact]
    public async Task CheckAsync_NoLinkedServers_InSampleDb()
    {
        var checker = new CompatibilityChecker(_fixture.Connection);

        var issues = await checker.CheckAsync();

        Assert.DoesNotContain(issues, i => i.IssueType == "Linked Server");
    }

    [Fact]
    public async Task CheckAsync_NoFilestream_InSampleDb()
    {
        var checker = new CompatibilityChecker(_fixture.Connection);

        var issues = await checker.CheckAsync();

        Assert.DoesNotContain(issues, i => i.IssueType == "FILESTREAM/FileTable");
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
