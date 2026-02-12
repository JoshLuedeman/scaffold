using Scaffold.Core.Enums;

namespace Scaffold.Assessment.SqlServer;

public static class CompatibilityMatrix
{
    private static readonly Dictionary<string, Dictionary<string, (CompatibilitySeverity Severity, string? DocUrl)>> Matrix =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["CLR Assembly"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/azure-sql/database/features-comparison#CLR"),

            ["Cross-Database Query"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/azure-sql/database/elastic-query-overview"),

            ["Service Broker"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/azure-sql/database/features-comparison"),

            ["Linked Server"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/azure-sql/database/features-comparison"),

            ["FILESTREAM/FileTable"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/data-migration/sql-server/database/assessment-rules#FileStream"),

            ["SQL Server Agent Job"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/azure-sql/database/elastic-jobs-overview"),

            ["Database Mail"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/data-migration/sql-server/database/assessment-rules#DatabaseMail"),

            ["Distributed Transaction"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Partial, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/azure-sql/database/elastic-transactions-overview"),

            ["PolyBase"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/data-migration/sql-server/managed-instance/assessment-rules"),

            ["Multiple Log Files"] = BuildRow(
                CompatibilitySeverity.Partial, CompatibilitySeverity.Partial,
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/data-migration/sql-server/managed-instance/assessment-rules"),

            ["Extended Stored Procedure"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/data-migration/sql-server/database/assessment-rules#XpCmdshell"),

            ["Replication (Transactional)"] = BuildRow(
                CompatibilitySeverity.Partial, CompatibilitySeverity.Partial,
                CompatibilitySeverity.Partial, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/azure-sql/database/replication-to-sql-database"),

            ["Replication (Merge)"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/azure-sql/managed-instance/replication-transactional-overview"),

            ["Availability Groups"] = BuildRow(
                CompatibilitySeverity.Partial, CompatibilitySeverity.Partial,
                CompatibilitySeverity.Partial, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/azure-sql/database/features-comparison"),

            ["SSIS Packages"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Partial, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/data-factory/concepts-integration-runtime"),

            ["Unsupported Data Type"] = BuildRow(
                CompatibilitySeverity.Partial, CompatibilitySeverity.Partial,
                CompatibilitySeverity.Partial, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/azure-sql/database/transact-sql-tsql-differences-sql-server"),

            ["BULK INSERT (Non-Azure)"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/data-migration/sql-server/database/assessment-rules#BulkInsert"),

            ["OPENROWSET (Non-Azure)"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/data-migration/sql-server/database/assessment-rules#OpenRowsetWithNonBlobDataSourceBulk"),

            ["Cryptographic Provider"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/data-migration/sql-server/database/assessment-rules#CryptographicProvider"),

            ["xp_cmdshell"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/data-migration/sql-server/database/assessment-rules#XpCmdshell"),

            ["Server-Scoped Trigger"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/data-migration/sql-server/database/assessment-rules#ServerScopedTriggers"),

            ["Server Audit"] = BuildRow(
                CompatibilitySeverity.Partial, CompatibilitySeverity.Partial,
                CompatibilitySeverity.Partial, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/azure-sql/database/auditing-overview"),

            ["Server Credential"] = BuildRow(
                CompatibilitySeverity.Partial, CompatibilitySeverity.Partial,
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/sql/t-sql/statements/create-database-scoped-credential-transact-sql"),

            ["Windows Authentication"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Partial, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/data-migration/sql-server/database/assessment-rules#WindowsAuthentication"),

            ["Trace Flags"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Partial, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/data-migration/sql-server/database/assessment-rules#TraceFlags"),

            ["Database Size > 100TB"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/azure-sql/database/resource-limits-vcore-single-databases"),

            ["COMPUTE Clause"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/data-migration/sql-server/database/assessment-rules#ComputeClause"),

            ["FASTFIRSTROW Hint"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/data-migration/sql-server/database/assessment-rules#FastFirstRowHint"),

            ["Legacy RAISERROR"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/data-migration/sql-server/database/assessment-rules#RAISERROR"),

            ["Non-ANSI Join Syntax"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Unsupported,
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/data-migration/sql-server/database/assessment-rules#NonANSILeftOuterJoinSyntax"),
        };

    private const string SqlDb = "Azure SQL Database";
    private const string Hyperscale = "Azure SQL Database Hyperscale";
    private const string ManagedInstance = "Azure SQL Managed Instance";
    private const string SqlOnVm = "SQL Server on Azure VM";

    public static CompatibilitySeverity GetSeverity(string issueType, string targetService)
    {
        if (Matrix.TryGetValue(issueType, out var targets) &&
            targets.TryGetValue(targetService, out var entry))
        {
            return entry.Severity;
        }

        return CompatibilitySeverity.Supported;
    }

    public static string? GetDocUrl(string issueType, string targetService)
    {
        if (Matrix.TryGetValue(issueType, out var targets) &&
            targets.TryGetValue(targetService, out var entry))
        {
            return entry.DocUrl;
        }

        return null;
    }

    private static Dictionary<string, (CompatibilitySeverity, string?)> BuildRow(
        CompatibilitySeverity sqlDb,
        CompatibilitySeverity hyperscale,
        CompatibilitySeverity managedInstance,
        CompatibilitySeverity sqlOnVm,
        string docUrl)
    {
        return new Dictionary<string, (CompatibilitySeverity, string?)>(StringComparer.OrdinalIgnoreCase)
        {
            [SqlDb] = (sqlDb, sqlDb != CompatibilitySeverity.Supported ? docUrl : null),
            [Hyperscale] = (hyperscale, hyperscale != CompatibilitySeverity.Supported ? docUrl : null),
            [ManagedInstance] = (managedInstance, managedInstance != CompatibilitySeverity.Supported ? docUrl : null),
            [SqlOnVm] = (sqlOnVm, sqlOnVm != CompatibilitySeverity.Supported ? docUrl : null),
        };
    }
}
