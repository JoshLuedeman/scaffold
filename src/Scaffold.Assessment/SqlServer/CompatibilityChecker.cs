using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Scaffold.Core.Enums;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.SqlServer;

public class CompatibilityChecker
{
    private readonly SqlConnection _connection;

    public CompatibilityChecker(SqlConnection connection)
    {
        _connection = connection;
    }

    public async Task<List<CompatibilityIssue>> CheckAsync(CancellationToken ct = default)
    {
        var issues = new List<CompatibilityIssue>();

        await CheckClrAssembliesAsync(issues, ct);
        await CheckCrossDatabaseQueriesAsync(issues, ct);
        await CheckServiceBrokerAsync(issues, ct);
        await CheckLinkedServersAsync(issues, ct);
        await CheckFilestreamAsync(issues, ct);
        await CheckAgentJobsAsync(issues, ct);
        await CheckUnsupportedDataTypesAsync(issues, ct);

        return issues;
    }

    public static double CalculateCompatibilityScore(List<CompatibilityIssue> issues)
    {
        const double blockingDeduction = 20.0;
        const double nonBlockingDeduction = 5.0;

        var score = 100.0;

        foreach (var issue in issues)
        {
            score -= issue.IsBlocking ? blockingDeduction : nonBlockingDeduction;
        }

        return Math.Max(0, score);
    }

    public static RiskRating DetermineRisk(List<CompatibilityIssue> issues, double score)
    {
        if (issues.Any(i => i.IsBlocking))
            return RiskRating.High;

        if (score < 80)
            return RiskRating.Medium;

        return RiskRating.Low;
    }

    private async Task CheckClrAssembliesAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sql = "SELECT name FROM sys.assemblies WHERE is_user_defined = 1";
        await using var cmd = new SqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            issues.Add(new CompatibilityIssue
            {
                ObjectName = reader.GetString(0),
                IssueType = "CLR Assembly",
                Description = "CLR assemblies are not supported in Azure SQL Database.",
                IsBlocking = true
            });
        }
    }

    private async Task CheckCrossDatabaseQueriesAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sql = """
            SELECT SCHEMA_NAME(o.schema_id) + '.' + o.name, m.definition
            FROM sys.sql_modules m
            JOIN sys.objects o ON m.object_id = o.object_id
            WHERE o.type IN ('P', 'FN', 'IF', 'TF', 'V', 'TR')
            """;
        await using var cmd = new SqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        // Matches three-part names like [db].[schema].[object] or db.schema.object
        var threePartNamePattern = new Regex(
            @"(?<!\w)(\[?\w+\]?\.\[?\w+\]?\.\[?\w+\]?)",
            RegexOptions.Compiled);

        while (await reader.ReadAsync(ct))
        {
            var objectName = reader.GetString(0);
            var definition = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);

            if (threePartNamePattern.IsMatch(definition))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = objectName,
                    IssueType = "Cross-Database Query",
                    Description = "Contains three-part name references which may indicate cross-database queries not supported in Azure SQL Database.",
                    IsBlocking = true
                });
            }
        }
    }

    private async Task CheckServiceBrokerAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sql = "SELECT name FROM sys.service_queues WHERE is_ms_shipped = 0";
        await using var cmd = new SqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            issues.Add(new CompatibilityIssue
            {
                ObjectName = reader.GetString(0),
                IssueType = "Service Broker",
                Description = "Service Broker queues are not fully supported in Azure SQL Database.",
                IsBlocking = true
            });
        }
    }

    private async Task CheckLinkedServersAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sql = "SELECT name FROM sys.servers WHERE is_linked = 1";
        await using var cmd = new SqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            issues.Add(new CompatibilityIssue
            {
                ObjectName = reader.GetString(0),
                IssueType = "Linked Server",
                Description = "Linked servers are not supported in Azure SQL Database.",
                IsBlocking = true
            });
        }
    }

    private async Task CheckFilestreamAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sql = """
            SELECT SCHEMA_NAME(schema_id) + '.' + name
            FROM sys.tables
            WHERE filestream_data_space_id IS NOT NULL AND filestream_data_space_id <> 0
            """;
        await using var cmd = new SqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            issues.Add(new CompatibilityIssue
            {
                ObjectName = reader.GetString(0),
                IssueType = "FILESTREAM/FileTable",
                Description = "FILESTREAM and FileTable features are not supported in Azure SQL Database.",
                IsBlocking = true
            });
        }
    }

    private async Task CheckAgentJobsAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        try
        {
            const string sql = "SELECT name FROM msdb.dbo.sysjobs";
            await using var cmd = new SqlCommand(sql, _connection);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = reader.GetString(0),
                    IssueType = "SQL Server Agent Job",
                    Description = "SQL Server Agent jobs are not available in Azure SQL Database. Consider Azure Elastic Jobs or Azure Automation.",
                    IsBlocking = false
                });
            }
        }
        catch
        {
            // msdb access may not be available — skip this check
        }
    }

    private async Task CheckUnsupportedDataTypesAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sql = """
            SELECT
                SCHEMA_NAME(t.schema_id) + '.' + t.name + '.' + c.name AS ColumnPath,
                ty.name AS DataType
            FROM sys.columns c
            JOIN sys.tables t ON c.object_id = t.object_id
            JOIN sys.types ty ON c.user_type_id = ty.user_type_id
            WHERE ty.name IN ('hierarchyid', 'sql_variant', 'geography', 'geometry')
            """;
        await using var cmd = new SqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var columnPath = reader.GetString(0);
            var dataType = reader.GetString(1);
            issues.Add(new CompatibilityIssue
            {
                ObjectName = columnPath,
                IssueType = "Unsupported Data Type",
                Description = $"Column uses '{dataType}' which may have limited support or require special handling in Azure SQL Database.",
                IsBlocking = false
            });
        }
    }
}
