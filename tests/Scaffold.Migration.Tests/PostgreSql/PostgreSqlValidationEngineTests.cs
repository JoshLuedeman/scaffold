using Scaffold.Core.Models;
using Scaffold.Migration.PostgreSql;
using Scaffold.Migration.SqlServer;

namespace Scaffold.Migration.Tests.PostgreSql;

public class PostgreSqlValidationEngineTests
{
    #region QuoteSqlName

    [Theory]
    [InlineData("dbo.Users", "[dbo].[Users]")]
    [InlineData("Sales.Orders", "[Sales].[Orders]")]
    [InlineData("[dbo].[Users]", "[dbo].[Users]")]
    [InlineData("Users", "[Users]")]
    [InlineData("[Users]", "[Users]")]
    [InlineData("Audit.Logs", "[Audit].[Logs]")]
    public void QuoteSqlName_FormatsCorrectly(string input, string expected)
    {
        Assert.Equal(expected, PostgreSqlValidationEngine.QuoteSqlName(input));
    }

    [Fact]
    public void QuoteSqlName_AlreadyQuoted_NoDoubleQuoting()
    {
        var result = PostgreSqlValidationEngine.QuoteSqlName("[dbo].[Products]");
        Assert.Equal("[dbo].[Products]", result);
    }

    #endregion

    #region QuotePgName — dbo→public mapping

    [Theory]
    [InlineData("dbo.Users", "\"public\".\"Users\"")]
    [InlineData("DBO.Users", "\"public\".\"Users\"")]
    [InlineData("[dbo].[Users]", "\"public\".\"Users\"")]
    [InlineData("Dbo.Customers", "\"public\".\"Customers\"")]
    public void QuotePgName_DboSchema_MapsToPublic(string input, string expected)
    {
        Assert.Equal(expected, PostgreSqlValidationEngine.QuotePgName(input));
    }

    #endregion

    #region QuotePgName — custom schemas preserved

    [Theory]
    [InlineData("Sales.Orders", "\"Sales\".\"Orders\"")]
    [InlineData("Audit.Logs", "\"Audit\".\"Logs\"")]
    [InlineData("[Sales].[Orders]", "\"Sales\".\"Orders\"")]
    [InlineData("Reporting.Metrics", "\"Reporting\".\"Metrics\"")]
    public void QuotePgName_CustomSchema_Preserved(string input, string expected)
    {
        Assert.Equal(expected, PostgreSqlValidationEngine.QuotePgName(input));
    }

    #endregion

    #region QuotePgName — single name (no schema)

    [Theory]
    [InlineData("Users", "\"Users\"")]
    [InlineData("[Users]", "\"Users\"")]
    public void QuotePgName_NoSchema_QuotesTableOnly(string input, string expected)
    {
        Assert.Equal(expected, PostgreSqlValidationEngine.QuotePgName(input));
    }

    #endregion

    #region QuotePgName — already PG-quoted input

    [Fact]
    public void QuotePgName_AlreadyPgQuoted_StripsAndRequotes()
    {
        var result = PostgreSqlValidationEngine.QuotePgName("\"public\".\"Users\"");
        Assert.Equal("\"public\".\"Users\"", result);
    }

    #endregion

    #region ValidationResult — pass/fail based on counts

    [Fact]
    public void ValidationResult_MatchingCounts_Passed()
    {
        var result = new ValidationResult
        {
            TableName = "dbo.Users",
            SourceRowCount = 500,
            TargetRowCount = 500,
            ChecksumMatch = true
        };

        Assert.True(result.Passed);
    }

    [Fact]
    public void ValidationResult_MismatchedCounts_Failed()
    {
        var result = new ValidationResult
        {
            TableName = "dbo.Users",
            SourceRowCount = 500,
            TargetRowCount = 499,
            ChecksumMatch = false
        };

        Assert.False(result.Passed);
    }

    [Fact]
    public void ValidationResult_TargetMoreRows_Failed()
    {
        var result = new ValidationResult
        {
            TableName = "dbo.Orders",
            SourceRowCount = 100,
            TargetRowCount = 101,
            ChecksumMatch = false
        };

        Assert.False(result.Passed);
    }

    [Fact]
    public void ValidationResult_ZeroCounts_Passed()
    {
        var result = new ValidationResult
        {
            TableName = "dbo.EmptyTable",
            SourceRowCount = 0,
            TargetRowCount = 0,
            ChecksumMatch = true
        };

        Assert.True(result.Passed);
    }

    [Fact]
    public void ValidationResult_CountsMatchButChecksumFalse_Failed()
    {
        var result = new ValidationResult
        {
            TableName = "dbo.Users",
            SourceRowCount = 100,
            TargetRowCount = 100,
            ChecksumMatch = false
        };

        Assert.False(result.Passed);
    }

    #endregion

    #region ValidationSummary construction

    [Fact]
    public void ValidationSummary_AllPassed_ReportsCorrectly()
    {
        var summary = new ValidationSummary
        {
            Results =
            [
                new() { TableName = "dbo.A", SourceRowCount = 10, TargetRowCount = 10, ChecksumMatch = true },
                new() { TableName = "dbo.B", SourceRowCount = 20, TargetRowCount = 20, ChecksumMatch = true },
                new() { TableName = "dbo.C", SourceRowCount = 0, TargetRowCount = 0, ChecksumMatch = true }
            ],
            TablesValidated = 3,
            TablesPassed = 3,
            TablesFailed = 0,
            AllPassed = true
        };

        Assert.True(summary.AllPassed);
        Assert.Equal(3, summary.TablesValidated);
        Assert.Equal(3, summary.TablesPassed);
        Assert.Equal(0, summary.TablesFailed);
    }

    [Fact]
    public void ValidationSummary_OneFailed_ReportsCorrectly()
    {
        var summary = new ValidationSummary
        {
            Results =
            [
                new() { TableName = "dbo.A", SourceRowCount = 10, TargetRowCount = 10, ChecksumMatch = true },
                new() { TableName = "dbo.B", SourceRowCount = 20, TargetRowCount = 19, ChecksumMatch = false }
            ],
            TablesValidated = 2,
            TablesPassed = 1,
            TablesFailed = 1,
            AllPassed = false
        };

        Assert.False(summary.AllPassed);
        Assert.Equal(1, summary.TablesFailed);
        Assert.Equal(1, summary.TablesPassed);
    }

    [Fact]
    public void ValidationSummary_AllFailed_ReportsCorrectly()
    {
        var summary = new ValidationSummary
        {
            Results =
            [
                new() { TableName = "dbo.A", SourceRowCount = 10, TargetRowCount = 5, ChecksumMatch = false },
                new() { TableName = "dbo.B", SourceRowCount = 20, TargetRowCount = 15, ChecksumMatch = false }
            ],
            TablesValidated = 2,
            TablesPassed = 0,
            TablesFailed = 2,
            AllPassed = false
        };

        Assert.False(summary.AllPassed);
        Assert.Equal(2, summary.TablesFailed);
        Assert.Equal(0, summary.TablesPassed);
    }

    [Fact]
    public void ValidationSummary_Empty_CanBeConstructed()
    {
        var summary = new ValidationSummary
        {
            Results = [],
            TablesValidated = 0,
            TablesPassed = 0,
            TablesFailed = 0,
            AllPassed = true
        };

        Assert.True(summary.AllPassed);
        Assert.Empty(summary.Results);
    }

    #endregion

    #region Cross-platform checksum behavior

    [Fact]
    public void CrossPlatformValidation_RowCountEquality_ImpliesChecksumMatch()
    {
        // In the PG validation engine, checksum match is derived from row count equality
        // since cross-platform checksums are not directly comparable.
        // This test documents that behavior.
        var matchingResult = new ValidationResult
        {
            TableName = "dbo.Users",
            SourceRowCount = 1000,
            TargetRowCount = 1000,
            ChecksumMatch = true // set when counts match
        };
        Assert.True(matchingResult.Passed);

        var mismatchedResult = new ValidationResult
        {
            TableName = "dbo.Users",
            SourceRowCount = 1000,
            TargetRowCount = 999,
            ChecksumMatch = false // set when counts differ
        };
        Assert.False(mismatchedResult.Passed);
    }

    #endregion
}