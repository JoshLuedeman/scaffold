using Scaffold.Assessment.PostgreSql;
using Scaffold.Core.Enums;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests.PostgreSql;

public class CompatibilityCheckerTests
{
    private static CompatibilityIssue Unsupported(string name = "obj") =>
        new() { ObjectName = name, IssueType = "Unsupported Extension", Description = "Blocked", IsBlocking = true, Severity = CompatibilitySeverity.Unsupported };

    private static CompatibilityIssue Partial(string name = "obj") =>
        new() { ObjectName = name, IssueType = "Foreign Data Wrappers", Description = "Warning", IsBlocking = false, Severity = CompatibilitySeverity.Partial };

    private static CompatibilityIssue Supported(string name = "obj") =>
        new() { ObjectName = name, IssueType = "PostGIS", Description = "OK", IsBlocking = false, Severity = CompatibilitySeverity.Supported };

    // -- CalculateCompatibilityScore ---------------------------------

    [Fact]
    public void NoIssues_ScoreIs100()
    {
        var score = CompatibilityChecker.CalculateCompatibilityScore([]);

        Assert.Equal(100.0, score);
    }

    [Fact]
    public void SingleUnsupportedIssue_Deducts5()
    {
        var issues = new List<CompatibilityIssue> { Unsupported() };
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);

        Assert.Equal(95.0, score);
    }

    [Fact]
    public void SinglePartialIssue_Deducts2()
    {
        var issues = new List<CompatibilityIssue> { Partial() };
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);

        Assert.Equal(98.0, score);
    }

    [Fact]
    public void MultipleUnsupportedIssues_DeductsCumulatively()
    {
        var issues = new List<CompatibilityIssue> { Unsupported("a"), Unsupported("b"), Unsupported("c") };
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);

        Assert.Equal(85.0, score);
    }

    [Fact]
    public void MixedIssues_DeductsCorrectly()
    {
        var issues = new List<CompatibilityIssue>
        {
            Unsupported("a"), // -5
            Partial("b")      // -2
        };
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);

        Assert.Equal(93.0, score);
    }

    [Fact]
    public void Score_NeverBelowZero()
    {
        var issues = Enumerable.Range(0, 25).Select(i => Unsupported($"obj{i}")).ToList();
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);

        Assert.Equal(0.0, score);
    }

    [Fact]
    public void SupportedIssues_NoDeduction()
    {
        var issues = new List<CompatibilityIssue> { Supported("a"), Supported("b") };
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);

        Assert.Equal(100.0, score);
    }

    // -- DetermineRisk -----------------------------------------------

    [Fact]
    public void NoIssues_RiskIsLow()
    {
        var risk = CompatibilityChecker.DetermineRisk([], 100.0);

        Assert.Equal(RiskRating.Low, risk);
    }

    [Fact]
    public void UnsupportedIssues_RiskIsHigh()
    {
        var issues = new List<CompatibilityIssue> { Unsupported() };
        var risk = CompatibilityChecker.DetermineRisk(issues, 95.0);

        Assert.Equal(RiskRating.High, risk);
    }

    [Fact]
    public void UnsupportedIssues_RiskIsHigh_RegardlessOfScore()
    {
        var issues = new List<CompatibilityIssue> { Unsupported() };
        var risk = CompatibilityChecker.DetermineRisk(issues, 99.0);

        Assert.Equal(RiskRating.High, risk);
    }

    [Fact]
    public void PartialIssues_ScoreBelow80_RiskIsMedium()
    {
        var issues = new List<CompatibilityIssue> { Partial() };
        var risk = CompatibilityChecker.DetermineRisk(issues, 75.0);

        Assert.Equal(RiskRating.Medium, risk);
    }

    [Fact]
    public void PartialIssues_ScoreAtOrAbove80_RiskIsLow()
    {
        var issues = new List<CompatibilityIssue> { Partial() };
        var risk = CompatibilityChecker.DetermineRisk(issues, 80.0);

        Assert.Equal(RiskRating.Low, risk);
    }

    [Fact]
    public void ScoreExactly80_NoUnsupported_RiskIsLow()
    {
        var issues = new List<CompatibilityIssue> { Partial() };
        var risk = CompatibilityChecker.DetermineRisk(issues, 80.0);

        Assert.Equal(RiskRating.Low, risk);
    }

    // -- ApplyTargetSeverity -----------------------------------------

    [Fact]
    public void ApplyTargetSeverity_FlexibleServer_SetsSeverityFromMatrix()
    {
        var issues = new List<CompatibilityIssue>
        {
            new() { ObjectName = "test", IssueType = "Superuser Access", Description = "test", Severity = CompatibilitySeverity.Supported },
            new() { ObjectName = "test", IssueType = "Custom C Extensions", Description = "test", Severity = CompatibilitySeverity.Supported },
        };

        CompatibilityChecker.ApplyTargetSeverity(issues, "Azure Database for PostgreSQL - Flexible Server");

        Assert.Equal(CompatibilitySeverity.Unsupported, issues[0].Severity);
        Assert.Equal(CompatibilitySeverity.Unsupported, issues[1].Severity);
        Assert.True(issues[0].IsBlocking);
        Assert.True(issues[1].IsBlocking);
    }

    [Fact]
    public void ApplyTargetSeverity_PgOnVm_MostThingsSupported()
    {
        var issues = new List<CompatibilityIssue>
        {
            new() { ObjectName = "test", IssueType = "Superuser Access", Description = "test", Severity = CompatibilitySeverity.Unsupported },
            new() { ObjectName = "test", IssueType = "Custom C Extensions", Description = "test", Severity = CompatibilitySeverity.Unsupported },
            new() { ObjectName = "test", IssueType = "Foreign Data Wrappers", Description = "test", Severity = CompatibilitySeverity.Partial },
        };

        CompatibilityChecker.ApplyTargetSeverity(issues, "PostgreSQL on Azure VM");

        Assert.All(issues, issue =>
        {
            Assert.Equal(CompatibilitySeverity.Supported, issue.Severity);
            Assert.False(issue.IsBlocking);
        });
    }

    [Fact]
    public void ApplyTargetSeverity_SetsDocUrl()
    {
        var issues = new List<CompatibilityIssue>
        {
            new() { ObjectName = "test", IssueType = "Superuser Access", Description = "test" }
        };

        CompatibilityChecker.ApplyTargetSeverity(issues, "Azure Database for PostgreSQL - Flexible Server");

        Assert.NotNull(issues[0].DocUrl);
        Assert.StartsWith("https://", issues[0].DocUrl);
    }

    [Fact]
    public void ApplyTargetSeverity_EmptyList_DoesNotThrow()
    {
        var issues = new List<CompatibilityIssue>();
        CompatibilityChecker.ApplyTargetSeverity(issues, "Azure Database for PostgreSQL - Flexible Server");
        Assert.Empty(issues);
    }

    [Theory]
    [InlineData("Azure Database for PostgreSQL - Flexible Server")]
    [InlineData("PostgreSQL on Azure VM")]
    public void ApplyTargetSeverity_AllTargetServices_DoNotThrow(string targetService)
    {
        var issues = new List<CompatibilityIssue>
        {
            new() { ObjectName = "test", IssueType = "Superuser Access", Description = "test" },
            new() { ObjectName = "test", IssueType = "Foreign Data Wrappers", Description = "test" },
            new() { ObjectName = "test", IssueType = "Unsupported Extension", Description = "test" },
        };

        CompatibilityChecker.ApplyTargetSeverity(issues, targetService);

        Assert.All(issues, issue =>
        {
            Assert.True(Enum.IsDefined(issue.Severity));
        });
    }

    // -- Scoring: 100 for no issues, lower for unsupported -----------

    [Fact]
    public void FullScenario_NoIssues_PerfectScore()
    {
        var issues = new List<CompatibilityIssue>();
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);
        var risk = CompatibilityChecker.DetermineRisk(issues, score);

        Assert.Equal(100.0, score);
        Assert.Equal(RiskRating.Low, risk);
    }

    [Fact]
    public void FullScenario_ManyUnsupported_LowScoreHighRisk()
    {
        var issues = Enumerable.Range(0, 10)
            .Select(i => Unsupported($"ext{i}"))
            .ToList();
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);
        var risk = CompatibilityChecker.DetermineRisk(issues, score);

        Assert.Equal(50.0, score);
        Assert.Equal(RiskRating.High, risk);
    }
}
