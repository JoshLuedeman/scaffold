using Scaffold.Core.Enums;

namespace Scaffold.Assessment.PostgreSql;

public static class CompatibilityMatrix
{
    private static readonly Dictionary<string, Dictionary<string, (CompatibilitySeverity Severity, string? DocUrl)>> Matrix =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Superuser Access"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-security"),

            ["Custom C Extensions"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-extensions"),

            ["Foreign Data Wrappers"] = BuildRow(
                CompatibilitySeverity.Partial, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-extensions"),

            ["Tablespace (Custom)"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-limits"),

            ["Event Triggers"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["Logical Replication"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["Partitioning"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["PostGIS"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["pg_cron"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["pgaudit"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["pg_trgm"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["hstore"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["uuid-ossp"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["pgcrypto"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["pg_stat_statements"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["Unsupported Extension"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-extensions"),

            ["Database Size > 16TB"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-limits"),

            ["PG Version < 12"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-supported-versions"),

            ["Custom Background Workers"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-limits"),

            ["COPY FROM PROGRAM"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-security"),

            ["File System Access (lo_import)"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-security"),

            ["Custom WAL Handlers"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-limits"),

            ["Deprecated Data Types (money precision)"] = BuildRow(
                CompatibilitySeverity.Partial, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-limits"),

            ["PL/Python"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-extensions"),

            ["PL/Perl"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-extensions"),

            ["PL/Tcl"] = BuildRow(
                CompatibilitySeverity.Unsupported, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-extensions"),

            ["Custom Collation"] = BuildRow(
                CompatibilitySeverity.Partial, CompatibilitySeverity.Supported,
                "https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-limits"),

            ["Multiple Databases Per Instance"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["Connection Pooling (PgBouncer)"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["Large Objects (> 1GB)"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["Full Text Search"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["XML Processing"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["JSONB"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["Array Types"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["Range Types"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["Composite Types"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),

            ["Domain Types"] = BuildRow(
                CompatibilitySeverity.Supported, CompatibilitySeverity.Supported,
                ""),
        };

    private const string FlexibleServer = "Azure Database for PostgreSQL - Flexible Server";
    private const string PgOnVm = "PostgreSQL on Azure VM";

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
        CompatibilitySeverity flexSeverity,
        CompatibilitySeverity vmSeverity,
        string docUrl)
    {
        return new Dictionary<string, (CompatibilitySeverity, string?)>(StringComparer.OrdinalIgnoreCase)
        {
            [FlexibleServer] = (flexSeverity, flexSeverity != CompatibilitySeverity.Supported ? docUrl : null),
            [PgOnVm] = (vmSeverity, vmSeverity != CompatibilitySeverity.Supported ? docUrl : null),
        };
    }
}
