using Npgsql;
using Scaffold.Core.Enums;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.PostgreSql;

public class CompatibilityChecker
{
    private readonly NpgsqlConnection _connection;

    public CompatibilityChecker(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    public async Task<List<CompatibilityIssue>> CheckAsync(CancellationToken ct = default)
    {
        var issues = new List<CompatibilityIssue>();

        await CheckExtensionsAsync(issues, ct);
        await CheckCustomTypesAsync(issues, ct);
        await CheckForeignDataWrappersAsync(issues, ct);
        await CheckTablespacesAsync(issues, ct);
        await CheckEventTriggersAsync(issues, ct);
        await CheckWalLevelAsync(issues, ct);
        await CheckDatabaseSizeAsync(issues, ct);
        await CheckPgVersionAsync(issues, ct);
        await CheckProceduralLanguagesAsync(issues, ct);

        return issues;
    }

    public static void ApplyTargetSeverity(List<CompatibilityIssue> issues, string targetService)
    {
        foreach (var issue in issues)
        {
            issue.Severity = CompatibilityMatrix.GetSeverity(issue.IssueType, targetService);
            issue.DocUrl = CompatibilityMatrix.GetDocUrl(issue.IssueType, targetService);
            issue.IsBlocking = issue.Severity == CompatibilitySeverity.Unsupported;
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

    // -- Known-supported extensions on Flexible Server ----------------

    private static readonly HashSet<string> KnownSupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "postgis", "postgis_raster", "postgis_sfcgal", "postgis_tiger_geocoder", "postgis_topology",
        "pg_cron", "pgaudit", "pg_trgm", "hstore", "uuid-ossp", "pgcrypto", "pg_stat_statements",
        "postgres_fdw", "btree_gin", "btree_gist", "citext", "cube", "dblink", "dict_int",
        "dict_xsyn", "earthdistance", "fuzzystrmatch", "hypopg", "intagg", "intarray",
        "isn", "lo", "ltree", "orafce", "pageinspect", "pg_buffercache", "pg_freespacemap",
        "pg_hint_plan", "pg_partman", "pg_prewarm", "pg_repack", "pg_visibility",
        "pglogical", "pgrouting", "pgrowlocks", "pgstattuple", "plv8",
        "semver", "sslinfo", "tablefunc", "timescaledb", "tsm_system_rows",
        "tsm_system_time", "unaccent", "vector", "address_standardizer",
        "address_standardizer_data_us", "amcheck", "bloom"
    };

    private async Task CheckExtensionsAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sql = "SELECT extname FROM pg_extension WHERE extname != 'plpgsql'";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var extName = reader.GetString(0);
            if (KnownSupportedExtensions.Contains(extName))
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = extName,
                    IssueType = extName,
                    Description = $"Extension '{extName}' is installed and supported on Azure Database for PostgreSQL - Flexible Server.",
                    IsBlocking = false,
                    Severity = CompatibilitySeverity.Supported
                });
            }
            else
            {
                issues.Add(new CompatibilityIssue
                {
                    ObjectName = extName,
                    IssueType = "Unsupported Extension",
                    Description = $"Extension '{extName}' is not supported on Azure Database for PostgreSQL - Flexible Server.",
                    IsBlocking = true,
                    Severity = CompatibilitySeverity.Unsupported
                });
            }
        }
    }

    private async Task CheckCustomTypesAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sql = """
            SELECT n.nspname, t.typname
            FROM pg_type t
            JOIN pg_namespace n ON t.typnamespace = n.oid
            WHERE t.typtype = 'c'
            AND n.nspname NOT IN ('pg_catalog', 'information_schema')
            """;
        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var schema = reader.GetString(0);
            var typeName = reader.GetString(1);
            issues.Add(new CompatibilityIssue
            {
                ObjectName = $"{schema}.{typeName}",
                IssueType = "Custom C Extensions",
                Description = $"Custom composite type '{schema}.{typeName}' may rely on custom C extensions not available on Flexible Server.",
                IsBlocking = false,
                Severity = CompatibilitySeverity.Unsupported
            });
        }
    }

    private async Task CheckForeignDataWrappersAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sql = "SELECT srvname FROM pg_foreign_server";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var serverName = reader.GetString(0);
            issues.Add(new CompatibilityIssue
            {
                ObjectName = serverName,
                IssueType = "Foreign Data Wrappers",
                Description = $"Foreign server '{serverName}' uses Foreign Data Wrappers. Only postgres_fdw is supported on Flexible Server.",
                IsBlocking = false,
                Severity = CompatibilitySeverity.Partial
            });
        }
    }

    private async Task CheckTablespacesAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sql = "SELECT spcname FROM pg_tablespace WHERE spcname NOT IN ('pg_default', 'pg_global')";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var tablespaceName = reader.GetString(0);
            issues.Add(new CompatibilityIssue
            {
                ObjectName = tablespaceName,
                IssueType = "Tablespace (Custom)",
                Description = $"Custom tablespace '{tablespaceName}' is not supported on Azure Database for PostgreSQL - Flexible Server.",
                IsBlocking = true,
                Severity = CompatibilitySeverity.Unsupported
            });
        }
    }

    private async Task CheckEventTriggersAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sql = "SELECT evtname FROM pg_event_trigger";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var triggerName = reader.GetString(0);
            issues.Add(new CompatibilityIssue
            {
                ObjectName = triggerName,
                IssueType = "Event Triggers",
                Description = $"Event trigger '{triggerName}' is supported on Azure Database for PostgreSQL - Flexible Server.",
                IsBlocking = false,
                Severity = CompatibilitySeverity.Supported
            });
        }
    }

    private async Task CheckWalLevelAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sql = "SHOW wal_level";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        var result = await cmd.ExecuteScalarAsync(ct);
        var walLevel = result?.ToString() ?? string.Empty;

        if (!walLevel.Equals("logical", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new CompatibilityIssue
            {
                ObjectName = "wal_level",
                IssueType = "Logical Replication",
                Description = $"WAL level is '{walLevel}', not 'logical'. Logical replication requires wal_level = logical.",
                IsBlocking = false,
                Severity = CompatibilitySeverity.Supported
            });
        }
    }

    private async Task CheckDatabaseSizeAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sql = "SELECT pg_database_size(current_database())";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        var result = await cmd.ExecuteScalarAsync(ct);
        var sizeBytes = Convert.ToInt64(result);

        const long sixteenTb = 16L * 1024 * 1024 * 1024 * 1024;
        if (sizeBytes > sixteenTb)
        {
            var sizeGb = sizeBytes / (1024.0 * 1024 * 1024);
            issues.Add(new CompatibilityIssue
            {
                ObjectName = "current_database",
                IssueType = "Database Size > 16TB",
                Description = $"Database size is {sizeGb:F1} GB, exceeding the 16 TB limit for Flexible Server.",
                IsBlocking = true,
                Severity = CompatibilitySeverity.Unsupported
            });
        }
    }

    private async Task CheckPgVersionAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sql = "SHOW server_version";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        var result = await cmd.ExecuteScalarAsync(ct);
        var versionStr = result?.ToString() ?? string.Empty;

        // Parse major version from strings like "11.22", "14.5 (Ubuntu)"
        var majorStr = versionStr.Split('.')[0];
        if (int.TryParse(majorStr, out var major) && major < 12)
        {
            issues.Add(new CompatibilityIssue
            {
                ObjectName = "server_version",
                IssueType = "PG Version < 12",
                Description = $"PostgreSQL version {versionStr} is below the minimum supported version (12) for Flexible Server.",
                IsBlocking = true,
                Severity = CompatibilitySeverity.Unsupported
            });
        }
    }

    private async Task CheckProceduralLanguagesAsync(List<CompatibilityIssue> issues, CancellationToken ct)
    {
        const string sql = "SELECT lanname FROM pg_language WHERE lanname NOT IN ('internal', 'c', 'sql', 'plpgsql')";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var langName = reader.GetString(0);
            var issueType = langName.ToLowerInvariant() switch
            {
                "plpythonu" or "plpython2u" or "plpython3u" => "PL/Python",
                "plperlu" or "plperl" => "PL/Perl",
                "pltcl" or "pltclu" => "PL/Tcl",
                _ => "Custom C Extensions"
            };

            issues.Add(new CompatibilityIssue
            {
                ObjectName = langName,
                IssueType = issueType,
                Description = $"Procedural language '{langName}' is not supported on Azure Database for PostgreSQL - Flexible Server.",
                IsBlocking = true,
                Severity = CompatibilitySeverity.Unsupported
            });
        }
    }
}
