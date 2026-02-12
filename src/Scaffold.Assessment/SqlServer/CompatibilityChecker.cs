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

        var modules = await LoadSqlModulesAsync(ct);

        await CheckClrAssembliesAsync(issues, ct);
        CheckCrossDatabaseQueries(issues, modules);
        await CheckServiceBrokerAsync(issues, ct);
        await CheckLinkedServersAsync(issues, ct);
        await CheckFilestreamAsync(issues, ct);
        await CheckAgentJobsAsync(issues, ct);
        await CheckUnsupportedDataTypesAsync(issues, ct);
        await CheckDatabaseMailAsync(issues, ct);
        CheckDistributedTransactions(issues, modules);
        CheckBulkInsert(issues, modules);
        CheckOpenRowset(issues, modules);
        CheckCryptographicProvider(issues, modules);
        CheckXpCmdshell(issues, modules);
        await CheckServerScopedTriggersAsync(issues, ct);
        await CheckServerAuditsAsync(issues, ct);
        await CheckServerCredentialsAsync(issues, ct);
        await CheckWindowsAuthAsync(issues, ct);
        await CheckTraceFlagsAsync(issues, ct);
        await CheckPolyBaseAsync(issues, ct);
        await CheckMultipleLogFilesAsync(issues, ct);
        await CheckReplicationAsync(issues, ct);
        await CheckAvailabilityGroupsAsync(issues, ct);
        CheckDeprecatedTSqlPatterns(issues, modules);
        await CheckExtendedStoredProceduresAsync(issues, ct);
        await CheckDatabaseSizeLimitAsync(issues, ct);
        await CheckSsisPackagesAsync(issues, ct);

        return issues;
    }

    public static void ApplyTargetSeverity(List<CompatibilityIssue> issues, string targetService)
    {
        foreach (var issue in issues)
        {
            issue.Severity = CompatibilityMatrix.GetSeverity(issue.IssueType, targetService);
            issue.DocUrl = CompatibilityMatrix.GetDocUrl(issue.IssueType, targetService);
            issue.IsBlocking = issue.Severity == CompatibilitySeverity.Unsupported;
            // Replace the generic "Azure SQL Database" with the actual target service name
            if (!string.IsNullOrEmpty(issue.Description))
            {
                issue.Description = issue.Description.Replace("Azure SQL Database", targetService);
            }
        }
    }

    public static double CalculateCompatibilityScore(List<CompatibilityIssue> issues)
    {
        var score = 100.0;
        foreach (var issue in issues)
        {
            score -= issue.Severity switch
            {
                CompatibilitySeverity.Unsupported => 5.0,
                CompatibilitySeverity.Partial => 2.0,
                _ => 0.0
            };
        }
        return Math.Max(0, score);
    }

    public static RiskRating DetermineRisk(List<CompatibilityIssue> issues, double score)
    {
        if (issues.Any(i => i.Severity == CompatibilitySeverity.Unsupported))
            return RiskRating.High;

        if (score < 80)
            return RiskRating.Medium;

        return RiskRating.Low;
    }

    private async Task<List<(string ObjectName, string Definition)>> LoadSqlModulesAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT SCHEMA_NAME(o.schema_id) + '.' + o.name, m.definition
            FROM sys.sql_modules m
            JOIN sys.objects o ON m.object_id = o.object_id
            WHERE o.type IN ('P', 'FN', 'IF', 'TF', 'V', 'TR')
            """;
        var modules = new List<(string ObjectName, string Definition)>();
        await using var cmd = new SqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var objectName = reader.GetString(0);
            var definition = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            modules.Add((objectName, definition));
        }
        return modules;
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

    private void CheckCrossDatabaseQueries(List<CompatibilityIssue> issues, List<(string ObjectName, string Definition)> modules)
    {
        // Matches three-part names like [db].[schema].[object] or db.schema.object,
        // but excludes known system prefixes (sys, INFORMATION_SCHEMA) that are not cross-db refs.
        var threePartNamePattern = new Regex(
            @"(?<!\w)(?!sys\b)(?!INFORMATION_SCHEMA\b)(\[?\w+\]?\.\[?\w+\]?\.\[?\w+\]?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        foreach (var (objectName, definition) in modules)
        {
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
            WHERE ty.name IN ('timestamp')
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

    private async Task CheckDatabaseMailAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        try
        {
            const string sql = "SELECT name FROM msdb.dbo.sysmail_profile";
            await using var cmd = new SqlCommand(sql, _connection);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = reader.GetString(0),
                    IssueType = "Database Mail",
                    Description = "Database uses Database Mail which is not available in Azure SQL Database. Consider Azure SQL Managed Instance.",
                    IsBlocking = false
                });
            }
        }
        catch
        {
            // msdb access may not be available — skip this check
        }
    }

    private void CheckDistributedTransactions(List<CompatibilityIssue> issues, List<(string ObjectName, string Definition)> modules)
    {
        foreach (var (objectName, definition) in modules)
        {
            if (definition.Contains("BEGIN DISTRIBUTED", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = objectName,
                    IssueType = "Distributed Transaction",
                    Description = "Uses distributed transactions which are not supported in Azure SQL Database.",
                    IsBlocking = false
                });
            }
        }
    }

    private void CheckBulkInsert(List<CompatibilityIssue> issues, List<(string ObjectName, string Definition)> modules)
    {
        foreach (var (objectName, definition) in modules)
        {
            if (definition.Contains("BULK INSERT", StringComparison.OrdinalIgnoreCase)
                && !definition.Contains("blob.core.windows.net", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = objectName,
                    IssueType = "BULK INSERT (Non-Azure)",
                    Description = "Uses BULK INSERT from a non-Azure source which is not supported in Azure SQL Database.",
                    IsBlocking = false
                });
            }
        }
    }

    private void CheckOpenRowset(List<CompatibilityIssue> issues, List<(string ObjectName, string Definition)> modules)
    {
        foreach (var (objectName, definition) in modules)
        {
            if (definition.Contains("OPENROWSET", StringComparison.OrdinalIgnoreCase)
                && !definition.Contains("blob.core.windows.net", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = objectName,
                    IssueType = "OPENROWSET (Non-Azure)",
                    Description = "Uses OPENROWSET from a non-Azure source which is not supported in Azure SQL Database.",
                    IsBlocking = false
                });
            }
        }
    }

    private void CheckCryptographicProvider(List<CompatibilityIssue> issues, List<(string ObjectName, string Definition)> modules)
    {
        foreach (var (objectName, definition) in modules)
        {
            if (definition.Contains("CREATE CRYPTOGRAPHIC PROVIDER", StringComparison.OrdinalIgnoreCase)
                || definition.Contains("ALTER CRYPTOGRAPHIC PROVIDER", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = objectName,
                    IssueType = "Cryptographic Provider",
                    Description = "Uses cryptographic providers which are not supported in Azure SQL Database.",
                    IsBlocking = false
                });
            }
        }
    }

    private void CheckXpCmdshell(List<CompatibilityIssue> issues, List<(string ObjectName, string Definition)> modules)
    {
        foreach (var (objectName, definition) in modules)
        {
            if (definition.Contains("xp_cmdshell", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = objectName,
                    IssueType = "xp_cmdshell",
                    Description = "Uses xp_cmdshell which is not available in Azure SQL Database.",
                    IsBlocking = false
                });
            }
        }
    }

    private async Task CheckServerScopedTriggersAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        try
        {
            const string sql = "SELECT name FROM sys.server_triggers WHERE is_ms_shipped = 0";
            await using var cmd = new SqlCommand(sql, _connection);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = reader.GetString(0),
                    IssueType = "Server-Scoped Trigger",
                    Description = "Server-scoped triggers are not supported in Azure SQL Database.",
                    IsBlocking = false
                });
            }
        }
        catch
        {
            // May require server-level permissions — skip this check
        }
    }

    private async Task CheckServerAuditsAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        try
        {
            const string sql = "SELECT name FROM sys.server_audits";
            await using var cmd = new SqlCommand(sql, _connection);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = reader.GetString(0),
                    IssueType = "Server Audit",
                    Description = "Server audits are not supported in Azure SQL Database. Consider Azure SQL auditing.",
                    IsBlocking = false
                });
            }
        }
        catch
        {
            // May require server-level permissions — skip this check
        }
    }

    private async Task CheckServerCredentialsAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        try
        {
            const string sql = "SELECT name FROM sys.credentials";
            await using var cmd = new SqlCommand(sql, _connection);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = reader.GetString(0),
                    IssueType = "Server Credential",
                    Description = "Server credentials are not supported in Azure SQL Database.",
                    IsBlocking = false
                });
            }
        }
        catch
        {
            // May require server-level permissions — skip this check
        }
    }

    private async Task CheckWindowsAuthAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sql = "SELECT name FROM sys.database_principals WHERE type = 'U' AND authentication_type = 3";
        await using var cmd = new SqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            issues.Add(new CompatibilityIssue
            {
                ObjectName = reader.GetString(0),
                IssueType = "Windows Authentication",
                Description = "Uses Windows Authentication which is not supported in Azure SQL Database. Consider Azure AD authentication.",
                IsBlocking = false
            });
        }
    }

    private async Task CheckTraceFlagsAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        try
        {
            const string sql = "DBCC TRACESTATUS(-1) WITH NO_INFOMSGS";
            await using var cmd = new SqlCommand(sql, _connection);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var status = reader.GetInt32(1);
                if (status == 1)
                {
                    var traceFlag = reader.GetInt32(0);
                    issues.Add(new CompatibilityIssue
                    {
                        ObjectName = $"TraceFlag {traceFlag}",
                        IssueType = "Trace Flags",
                        Description = $"Trace flag {traceFlag} is active and not supported in Azure SQL Database.",
                        IsBlocking = false
                    });
                }
            }
        }
        catch
        {
            // DBCC TRACESTATUS may not be available — skip this check
        }
    }

    private async Task CheckPolyBaseAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sqlDataSources = "SELECT name FROM sys.external_data_sources";
        await using (var cmd = new SqlCommand(sqlDataSources, _connection))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = reader.GetString(0),
                    IssueType = "PolyBase",
                    Description = "Uses PolyBase external data sources which may not be fully supported in Azure SQL Database.",
                    IsBlocking = false
                });
            }
        }

        const string sqlTables = "SELECT name FROM sys.external_tables";
        await using (var cmd = new SqlCommand(sqlTables, _connection))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = reader.GetString(0),
                    IssueType = "PolyBase",
                    Description = "Uses PolyBase external tables which may not be fully supported in Azure SQL Database.",
                    IsBlocking = false
                });
            }
        }
    }

    private async Task CheckMultipleLogFilesAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        try
        {
            const string sql = """
                SELECT DB_NAME(database_id) AS DbName, COUNT(*) AS LogFileCount
                FROM sys.master_files
                WHERE type = 1 AND database_id = DB_ID()
                GROUP BY database_id
                HAVING COUNT(*) > 1
                """;
            await using var cmd = new SqlCommand(sql, _connection);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var dbName = reader.GetString(0);
                var count = reader.GetInt32(1);
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = dbName,
                    IssueType = "Multiple Log Files",
                    Description = $"Database has {count} log files. Azure SQL Database supports only one log file.",
                    IsBlocking = false
                });
            }
        }
        catch
        {
            // sys.master_files access may not be available — skip this check
        }
    }

    private async Task CheckReplicationAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sql = """
            SELECT name, is_published, is_merge_published, is_subscribed
            FROM sys.databases WHERE database_id = DB_ID()
            """;
        await using var cmd = new SqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var dbName = reader.GetString(0);
            var isPublished = reader.GetBoolean(1);
            var isMergePublished = reader.GetBoolean(2);
            var isSubscribed = reader.GetBoolean(3);

            if (isPublished || isSubscribed)
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = dbName,
                    IssueType = "Replication (Transactional)",
                    Description = "Database uses transactional replication which is not supported in Azure SQL Database.",
                    IsBlocking = false
                });
            }

            if (isMergePublished)
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = dbName,
                    IssueType = "Replication (Merge)",
                    Description = "Database uses merge replication which is not supported in Azure SQL Database.",
                    IsBlocking = false
                });
            }
        }
    }

    private async Task CheckAvailabilityGroupsAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        try
        {
            const string sql = """
                SELECT ag.name
                FROM sys.dm_hadr_availability_group_states ags
                JOIN sys.availability_groups ag ON ags.group_id = ag.group_id
                """;
            await using var cmd = new SqlCommand(sql, _connection);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = reader.GetString(0),
                    IssueType = "Availability Groups",
                    Description = "Uses Always On Availability Groups which are not applicable in Azure SQL Database.",
                    IsBlocking = false
                });
            }
        }
        catch
        {
            // Availability Groups DMVs may not be available — skip this check
        }
    }

    private void CheckDeprecatedTSqlPatterns(List<CompatibilityIssue> issues, List<(string ObjectName, string Definition)> modules)
    {
        foreach (var (objectName, definition) in modules)
        {
            if (definition.Contains("COMPUTE ", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = objectName,
                    IssueType = "COMPUTE Clause",
                    Description = "Uses deprecated COMPUTE clause which is not supported in Azure SQL Database.",
                    IsBlocking = false
                });
            }

            if (definition.Contains("FASTFIRSTROW", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = objectName,
                    IssueType = "FASTFIRSTROW Hint",
                    Description = "Uses deprecated FASTFIRSTROW hint. Use OPTION (FAST n) instead.",
                    IsBlocking = false
                });
            }

            if (ContainsLegacyRaiserror(definition))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = objectName,
                    IssueType = "Legacy RAISERROR",
                    Description = "Uses legacy RAISERROR syntax. Use RAISERROR(...) with parentheses instead.",
                    IsBlocking = false
                });
            }

            if (definition.Contains("*=", StringComparison.Ordinal)
                || definition.Contains("=*", StringComparison.Ordinal))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = objectName,
                    IssueType = "Non-ANSI Join Syntax",
                    Description = "Uses deprecated non-ANSI join syntax (*= or =*). Use standard JOIN syntax instead.",
                    IsBlocking = false
                });
            }
        }
    }

    private static bool ContainsLegacyRaiserror(string definition)
    {
        // Legacy RAISERROR syntax: RAISERROR followed by a digit (not parenthesis)
        var idx = 0;
        while (idx < definition.Length)
        {
            idx = definition.IndexOf("RAISERROR ", idx, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) break;
            var afterIdx = idx + "RAISERROR ".Length;
            while (afterIdx < definition.Length && char.IsWhiteSpace(definition[afterIdx]))
                afterIdx++;
            if (afterIdx < definition.Length && char.IsDigit(definition[afterIdx]))
                return true;
            idx = afterIdx;
        }
        return false;
    }

    private async Task CheckExtendedStoredProceduresAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sql = "SELECT name FROM sys.objects WHERE type = 'X' AND is_ms_shipped = 0";
        await using var cmd = new SqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            issues.Add(new CompatibilityIssue
            {
                ObjectName = reader.GetString(0),
                IssueType = "Extended Stored Procedure",
                Description = "Extended stored procedures are not supported in Azure SQL Database.",
                IsBlocking = false
            });
        }
    }

    private async Task CheckDatabaseSizeLimitAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sql = "SELECT SUM(size) * 8.0 / 1024 / 1024 AS SizeGB FROM sys.database_files";
        await using var cmd = new SqlCommand(sql, _connection);
        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is double sizeGb && sizeGb > 102400)
        {
            issues.Add(new CompatibilityIssue
            {
                ObjectName = "Database",
                IssueType = "Database Size > 100TB",
                Description = $"Database size is {sizeGb:N0} GB which exceeds the 100 TB limit for Azure SQL Database.",
                IsBlocking = false
            });
        }
    }

    private async Task CheckSsisPackagesAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        try
        {
            const string sql = "SELECT name FROM msdb.dbo.sysssispackages";
            await using var cmd = new SqlCommand(sql, _connection);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = reader.GetString(0),
                    IssueType = "SSIS Packages",
                    Description = "SSIS packages are not supported in Azure SQL Database. Consider Azure Data Factory.",
                    IsBlocking = false
                });
            }
        }
        catch
        {
            // msdb access may not be available — skip this check
        }
    }
}
