namespace Scaffold.Migration.PostgreSql;

/// <summary>
/// Single source of truth for Azure Database for PostgreSQL - Flexible Server
/// supported extensions. Referenced by both Assessment (ExtensionDetector) and
/// Migration (AzureExtensionHandler).
/// </summary>
public static class AzurePostgreSqlExtensions
{
    /// <summary>
    /// Extensions supported on Azure PG Flexible Server as of 2024.
    /// </summary>
    public static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
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

    /// <summary>
    /// Extensions that require shared_preload_libraries configuration.
    /// These can't simply be installed via CREATE EXTENSION — the server
    /// parameter must be set first (requires restart on Azure).
    /// </summary>
    public static readonly HashSet<string> RequiresPreload = new(StringComparer.OrdinalIgnoreCase)
    {
        "pg_cron", "pg_stat_statements", "pg_hint_plan", "pgaudit",
        "pg_partman", "timescaledb", "pglogical"
    };

    /// <summary>
    /// Known extension dependency chains. Key depends on values.
    /// Install values before key.
    /// </summary>
    public static readonly Dictionary<string, string[]> Dependencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["postgis_raster"] = ["postgis"],
        ["postgis_sfcgal"] = ["postgis"],
        ["postgis_tiger_geocoder"] = ["postgis", "fuzzystrmatch"],
        ["postgis_topology"] = ["postgis"],
        ["address_standardizer_data_us"] = ["address_standardizer"],
        ["earthdistance"] = ["cube"],
        ["pgrouting"] = ["postgis"]
    };

    public static bool IsSupported(string extensionName) => Supported.Contains(extensionName);

    public static bool RequiresSharedPreload(string extensionName) => RequiresPreload.Contains(extensionName);

    public static string[] GetDependencies(string extensionName) =>
        Dependencies.TryGetValue(extensionName, out var deps) ? deps : [];
}
