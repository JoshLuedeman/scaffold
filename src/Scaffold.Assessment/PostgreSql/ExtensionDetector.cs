namespace Scaffold.Assessment.PostgreSql;

public class ExtensionDetector
{
    // Map of extensions known to be supported on Azure PG Flexible Server
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "address_standardizer", "address_standardizer_data_us", "amcheck", "bloom",
        "btree_gin", "btree_gist", "citext", "cube", "dblink", "dict_int",
        "dict_xsyn", "earthdistance", "fuzzystrmatch", "hstore", "hypopg",
        "intagg", "intarray", "isn", "lo", "ltree", "orafce",
        "pageinspect", "pg_buffercache", "pg_cron", "pg_freespacemap",
        "pg_hint_plan", "pg_partman", "pg_prewarm", "pg_repack",
        "pg_stat_statements", "pg_trgm", "pg_visibility", "pgaudit",
        "pgcrypto", "pglogical", "pgrouting", "pgrowlocks", "pgstattuple",
        "plpgsql", "plv8", "postgis", "postgis_raster", "postgis_sfcgal",
        "postgis_tiger_geocoder", "postgis_topology", "postgres_fdw",
        "semver", "sslinfo", "tablefunc", "timescaledb", "tsm_system_rows",
        "tsm_system_time", "unaccent", "uuid-ossp", "vector"
    };

    public static ExtensionCompatibilityResult Evaluate(string extensionName)
    {
        var isSupported = SupportedExtensions.Contains(extensionName);
        return new ExtensionCompatibilityResult
        {
            ExtensionName = extensionName,
            IsSupported = isSupported,
            Status = isSupported ? "Compatible" : "Incompatible",
            Recommendation = isSupported
                ? null
                : $"Extension '{extensionName}' is not supported on Azure Database for PostgreSQL - Flexible Server. Consider using PostgreSQL on Azure VM or finding an alternative."
        };
    }

    public static List<ExtensionCompatibilityResult> EvaluateAll(IEnumerable<string> extensionNames)
    {
        return extensionNames.Select(Evaluate).ToList();
    }
}

public class ExtensionCompatibilityResult
{
    public string ExtensionName { get; set; } = string.Empty;
    public bool IsSupported { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Recommendation { get; set; }
}
