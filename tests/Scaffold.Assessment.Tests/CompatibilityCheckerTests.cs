using Scaffold.Assessment.SqlServer;
using Scaffold.Core.Enums;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests;

public class CompatibilityCheckerTests
{
    private static CompatibilityIssue Unsupported(string name = "obj") =>
        new() { ObjectName = name, IssueType = "CLR Assembly", Description = "Blocked", IsBlocking = true, Severity = CompatibilitySeverity.Unsupported };

    private static CompatibilityIssue Partial(string name = "obj") =>
        new() { ObjectName = name, IssueType = "Unsupported Data Type", Description = "Warning", IsBlocking = false, Severity = CompatibilitySeverity.Partial };

    private static CompatibilityIssue Supported(string name = "obj") =>
        new() { ObjectName = name, IssueType = "Info", Description = "OK", IsBlocking = false, Severity = CompatibilitySeverity.Supported };

    // ── CalculateCompatibilityScore ─────────────────────────────────

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
        // 25 unsupported issues = 125 deduction, but floor is 0
        var issues = Enumerable.Range(0, 25).Select(i => Unsupported($"obj{i}")).ToList();
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);

        Assert.Equal(0.0, score);
    }

    [Fact]
    public void ManyPartialIssues_ScoreFloorsAtZero()
    {
        // 55 partial = 110 deduction, floor at 0
        var issues = Enumerable.Range(0, 55).Select(i => Partial($"obj{i}")).ToList();
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

    // ── DetermineRisk ───────────────────────────────────────────────

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

    [Fact]
    public void ScoreJustBelow80_NoUnsupported_RiskIsMedium()
    {
        var issues = new List<CompatibilityIssue> { Partial() };
        var risk = CompatibilityChecker.DetermineRisk(issues, 79.99);

        Assert.Equal(RiskRating.Medium, risk);
    }

    // ── Combined score + risk integration ───────────────────────────

    [Fact]
    public void TenPartial_ScoreIs80_RiskIsLow()
    {
        var issues = Enumerable.Range(0, 10).Select(i => Partial($"obj{i}")).ToList();
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);
        var risk = CompatibilityChecker.DetermineRisk(issues, score);

        Assert.Equal(80.0, score);
        Assert.Equal(RiskRating.Low, risk);
    }

    [Fact]
    public void ElevenPartial_ScoreIs78_RiskIsMedium()
    {
        var issues = Enumerable.Range(0, 11).Select(i => Partial($"obj{i}")).ToList();
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);
        var risk = CompatibilityChecker.DetermineRisk(issues, score);

        Assert.Equal(78.0, score);
        Assert.Equal(RiskRating.Medium, risk);
    }

    [Fact]
    public void OneUnsupported_AlwaysHigh_EvenIfScoreHigh()
    {
        var issues = new List<CompatibilityIssue> { Unsupported() };
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);
        var risk = CompatibilityChecker.DetermineRisk(issues, score);

        Assert.Equal(95.0, score);
        Assert.Equal(RiskRating.High, risk);
    }
}
