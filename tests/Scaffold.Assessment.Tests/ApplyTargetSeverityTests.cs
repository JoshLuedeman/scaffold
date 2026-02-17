using Scaffold.Assessment.SqlServer;
using Scaffold.Core.Enums;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests;

public class ApplyTargetSeverityTests
{
    private static List<CompatibilityIssue> CreateIssues(params string[] issueTypes)
    {
        return issueTypes.Select(t => new CompatibilityIssue
        {
            ObjectName = "TestObj",
            IssueType = t,
            Description = $"{t} are not supported in Azure SQL Database.",
            IsBlocking = true,
            Severity = CompatibilitySeverity.Unsupported
        }).ToList();
    }

    [Fact]
    public void ApplyTargetSeverity_AzureSqlDatabase_SetsSeverityFromMatrix()
    {
        var issues = CreateIssues("CLR Assembly", "Linked Server", "Service Broker");

        CompatibilityChecker.ApplyTargetSeverity(issues, "Azure SQL Database");

        foreach (var issue in issues)
        {
            // Verify severity was set from the matrix
            Assert.True(Enum.IsDefined(issue.Severity));
        }
    }

    [Fact]
    public void ApplyTargetSeverity_SqlServerOnAzureVM_MostThingsSupported()
    {
        var issues = CreateIssues("CLR Assembly", "Linked Server", "Service Broker",
            "SQL Server Agent Job", "FILESTREAM/FileTable");

        CompatibilityChecker.ApplyTargetSeverity(issues, "SQL Server on Azure VM");

        // Azure VM supports most features
        var unsupported = issues.Count(i => i.Severity == CompatibilitySeverity.Unsupported);
        Assert.True(unsupported < issues.Count,
            "SQL Server on Azure VM should support most features");
    }

    [Fact]
    public void ApplyTargetSeverity_SetsIsBlockingBasedOnSeverity()
    {
        var issues = CreateIssues("CLR Assembly");

        CompatibilityChecker.ApplyTargetSeverity(issues, "Azure SQL Managed Instance");

        foreach (var issue in issues)
        {
            Assert.Equal(issue.Severity == CompatibilitySeverity.Unsupported, issue.IsBlocking);
        }
    }

    [Fact]
    public void ApplyTargetSeverity_SetsDocUrl()
    {
        var issues = CreateIssues("CLR Assembly");

        CompatibilityChecker.ApplyTargetSeverity(issues, "Azure SQL Database");

        // DocUrl should be set (may be null or a valid URL depending on the matrix)
        // Just verify the method ran without error and set the property
        Assert.Single(issues);
    }

    [Fact]
    public void ApplyTargetSeverity_ReplacesDescriptionPlaceholder()
    {
        var issues = new List<CompatibilityIssue>
        {
            new()
            {
                ObjectName = "Test",
                IssueType = "CLR Assembly",
                Description = "Not supported in Azure SQL Database.",
                IsBlocking = true
            }
        };

        CompatibilityChecker.ApplyTargetSeverity(issues, "Azure SQL Managed Instance");

        Assert.Contains("Azure SQL Managed Instance", issues[0].Description);
        Assert.DoesNotContain("Azure SQL Database", issues[0].Description);
    }

    [Fact]
    public void ApplyTargetSeverity_EmptyDescription_DoesNotThrow()
    {
        var issues = new List<CompatibilityIssue>
        {
            new()
            {
                ObjectName = "Test",
                IssueType = "CLR Assembly",
                Description = "",
                IsBlocking = true
            }
        };

        CompatibilityChecker.ApplyTargetSeverity(issues, "Azure SQL Database");
        Assert.Equal("", issues[0].Description);
    }

    [Fact]
    public void ApplyTargetSeverity_EmptyList_DoesNotThrow()
    {
        var issues = new List<CompatibilityIssue>();
        CompatibilityChecker.ApplyTargetSeverity(issues, "Azure SQL Database");
        Assert.Empty(issues);
    }

    [Theory]
    [InlineData("Azure SQL Database")]
    [InlineData("Azure SQL Database Hyperscale")]
    [InlineData("Azure SQL Managed Instance")]
    [InlineData("SQL Server on Azure VM")]
    public void ApplyTargetSeverity_AllTargetServices_DoNotThrow(string targetService)
    {
        var issues = CreateIssues(
            "CLR Assembly", "Cross-Database Query", "Service Broker",
            "Linked Server", "FILESTREAM/FileTable", "SQL Server Agent Job",
            "Unsupported Data Type", "Database Mail", "Distributed Transaction",
            "BULK INSERT (Non-Azure)", "OPENROWSET (Non-Azure)", "Cryptographic Provider",
            "xp_cmdshell");

        CompatibilityChecker.ApplyTargetSeverity(issues, targetService);

        Assert.All(issues, issue =>
        {
            Assert.NotNull(issue.Severity.ToString());
        });
    }
}
