using Npgsql;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.PostgreSql;

public static class PerformanceProfiler
{
    public static async Task<PerformanceProfile> CollectAsync(NpgsqlConnection connection, CancellationToken ct = default)
    {
        var profile = new PerformanceProfile();

        // Database size
        await using (var cmd = new NpgsqlCommand("SELECT pg_database_size(current_database())", connection))
        {
            var size = await cmd.ExecuteScalarAsync(ct);
            profile.MaxDatabaseSizeMb = Convert.ToInt64(size) / (1024 * 1024);
        }

        // Connection count (proxy for CPU load)
        await using (var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM pg_stat_activity WHERE datname = current_database()", connection))
        {
            var connections = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            // Estimate CPU% from active connections relative to max_connections
            profile.AvgCpuPercent = Math.Min(100, connections * 5.0);
        }

        // Cache hit ratio (proxy for memory efficiency)
        await using (var cmd = new NpgsqlCommand(@"
            SELECT 
                CASE WHEN (blks_hit + blks_read) = 0 THEN 0 
                ELSE ROUND(blks_hit * 100.0 / (blks_hit + blks_read), 2) END as cache_hit_ratio,
                COALESCE(pg_database_size(current_database()) / (1024 * 1024), 0) as shared_buffers_mb
            FROM pg_stat_database 
            WHERE datname = current_database()", connection))
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var cacheHitRatio = reader.GetDouble(0);
                profile.MemoryUsedMb = reader.GetInt64(1); // Use DB size as proxy
            }
        }

        // IO estimate from block reads
        await using (var cmd = new NpgsqlCommand(@"
            SELECT COALESCE(blks_read * 8.0 / 1024 / GREATEST(EXTRACT(EPOCH FROM (now() - stats_reset)), 1), 0)
            FROM pg_stat_database WHERE datname = current_database()", connection))
        {
            var io = await cmd.ExecuteScalarAsync(ct);
            profile.AvgIoMbPerSecond = io != null && io != DBNull.Value ? Convert.ToDouble(io) : 0;
        }

        return profile;
    }
}
