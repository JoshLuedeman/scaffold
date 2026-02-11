using Scaffold.Assessment.SqlServer;
using Scaffold.Core.Enums;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests;

public class CompatibilityCheckerTests
{
    private static CompatibilityIssue Blocking(string name = "obj") =>
        new() { ObjectName = name, IssueType = "CLR Assembly", Description = "Blocked", IsBlocking = true };

    private static CompatibilityIssue NonBlocking(string name = "obj") =>
        new() { ObjectName = name, IssueType = "Agent Job", Description = "Warning", IsBlocking = false };

    // ── CalculateCompatibilityScore ─────────────────────────────────

    [Fact]
    public void NoIssues_ScoreIs100()
    {
        var score = CompatibilityChecker.CalculateCompatibilityScore([]);

        Assert.Equal(100.0, score);
    }

    [Fact]
    public void SingleBlockingIssue_Deducts20()
    {
        var issues = new List<CompatibilityIssue> { Blocking() };
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);

        Assert.Equal(80.0, score);
    }

    [Fact]
    public void SingleNonBlockingIssue_Deducts5()
    {
        var issues = new List<CompatibilityIssue> { NonBlocking() };
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);

        Assert.Equal(95.0, score);
    }

    [Fact]
    public void MultipleBlockingIssues_DeductsCumulatively()
    {
        var issues = new List<CompatibilityIssue> { Blocking("a"), Blocking("b"), Blocking("c") };
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);

        Assert.Equal(40.0, score);
    }

    [Fact]
    public void MixedIssues_DeductsCorrectly()
    {
        var issues = new List<CompatibilityIssue>
        {
            Blocking("a"),   // -20
            NonBlocking("b") // -5
        };
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);

        Assert.Equal(75.0, score);
    }

    [Fact]
    public void Score_NeverBelowZero()
    {
        // 6 blocking issues = 120 deduction, but floor is 0
        var issues = Enumerable.Range(0, 6).Select(i => Blocking($"obj{i}")).ToList();
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);

        Assert.Equal(0.0, score);
    }

    [Fact]
    public void ManyNonBlockingIssues_ScoreFloorsAtZero()
    {
        // 25 non-blocking = 125 deduction, floor at 0
        var issues = Enumerable.Range(0, 25).Select(i => NonBlocking($"obj{i}")).ToList();
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);

        Assert.Equal(0.0, score);
    }

    // ── DetermineRisk ───────────────────────────────────────────────

    [Fact]
    public void NoIssues_RiskIsLow()
    {
        var risk = CompatibilityChecker.DetermineRisk([], 100.0);

        Assert.Equal(RiskRating.Low, risk);
    }

    [Fact]
    public void BlockingIssues_RiskIsHigh()
    {
        var issues = new List<CompatibilityIssue> { Blocking() };
        var risk = CompatibilityChecker.DetermineRisk(issues, 80.0);

        Assert.Equal(RiskRating.High, risk);
    }

    [Fact]
    public void BlockingIssues_RiskIsHigh_RegardlessOfScore()
    {
        var issues = new List<CompatibilityIssue> { Blocking() };
        var risk = CompatibilityChecker.DetermineRisk(issues, 99.0);

        Assert.Equal(RiskRating.High, risk);
    }

    [Fact]
    public void NonBlockingIssues_ScoreBelow80_RiskIsMedium()
    {
        var issues = new List<CompatibilityIssue> { NonBlocking() };
        var risk = CompatibilityChecker.DetermineRisk(issues, 75.0);

        Assert.Equal(RiskRating.Medium, risk);
    }

    [Fact]
    public void NonBlockingIssues_ScoreAtOrAbove80_RiskIsLow()
    {
        var issues = new List<CompatibilityIssue> { NonBlocking() };
        var risk = CompatibilityChecker.DetermineRisk(issues, 80.0);

        Assert.Equal(RiskRating.Low, risk);
    }

    [Fact]
    public void ScoreExactly80_NoBlocking_RiskIsLow()
    {
        var issues = new List<CompatibilityIssue> { NonBlocking() };
        var risk = CompatibilityChecker.DetermineRisk(issues, 80.0);

        Assert.Equal(RiskRating.Low, risk);
    }

    [Fact]
    public void ScoreJustBelow80_NoBlocking_RiskIsMedium()
    {
        var issues = new List<CompatibilityIssue> { NonBlocking() };
        var risk = CompatibilityChecker.DetermineRisk(issues, 79.99);

        Assert.Equal(RiskRating.Medium, risk);
    }

    // ── Combined score + risk integration ───────────────────────────

    [Fact]
    public void FourNonBlocking_ScoreIs80_RiskIsLow()
    {
        var issues = Enumerable.Range(0, 4).Select(i => NonBlocking($"obj{i}")).ToList();
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);
        var risk = CompatibilityChecker.DetermineRisk(issues, score);

        Assert.Equal(80.0, score);
        Assert.Equal(RiskRating.Low, risk);
    }

    [Fact]
    public void FiveNonBlocking_ScoreIs75_RiskIsMedium()
    {
        var issues = Enumerable.Range(0, 5).Select(i => NonBlocking($"obj{i}")).ToList();
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);
        var risk = CompatibilityChecker.DetermineRisk(issues, score);

        Assert.Equal(75.0, score);
        Assert.Equal(RiskRating.Medium, risk);
    }

    [Fact]
    public void OneBlocking_AlwaysHigh_EvenIfScoreHigh()
    {
        var issues = new List<CompatibilityIssue> { Blocking() };
        var score = CompatibilityChecker.CalculateCompatibilityScore(issues);
        var risk = CompatibilityChecker.DetermineRisk(issues, score);

        Assert.Equal(80.0, score);
        Assert.Equal(RiskRating.High, risk);
    }
}
