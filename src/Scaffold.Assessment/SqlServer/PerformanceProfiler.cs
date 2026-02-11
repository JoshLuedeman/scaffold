using Microsoft.Data.SqlClient;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.SqlServer;

public static class PerformanceProfiler
{
    public static async Task<PerformanceProfile> CollectAsync(SqlConnection connection, CancellationToken ct = default)
    {
        var profile = new PerformanceProfile
        {
            AvgCpuPercent = await GetCpuPercentAsync(connection, ct),
            MemoryUsedMb = await GetMemoryUsedMbAsync(connection, ct),
            AvgIoMbPerSecond = await GetAvgIoMbPerSecondAsync(connection, ct),
            MaxDatabaseSizeMb = await GetDatabaseSizeMbAsync(connection, ct)
        };
        return profile;
    }

    private static async Task<double> GetCpuPercentAsync(SqlConnection connection, CancellationToken ct)
    {
        // Azure SQL exposes dm_db_resource_stats; fall back to dm_os_ring_buffers for on-prem
        try
        {
            const string sql = "SELECT AVG(avg_cpu_percent) FROM sys.dm_db_resource_stats;";
            await using var cmd = new SqlCommand(sql, connection);
            var result = await cmd.ExecuteScalarAsync(ct);
            if (result is not null and not DBNull)
                return Convert.ToDouble(result);
        }
        catch
        {
            // DMV not available — try on-prem fallback
        }

        try
        {
            const string sql = """
                SELECT TOP 1
                    record.value('(./Record/SchedulerMonitorEvent/SystemHealth/SystemIdle)[1]', 'int') AS SystemIdle
                FROM (
                    SELECT CONVERT(xml, record) AS record
                    FROM sys.dm_os_ring_buffers
                    WHERE ring_buffer_type = N'RING_BUFFER_SCHEDULER_MONITOR'
                      AND record LIKE N'%<SystemHealth>%'
                ) AS x
                ORDER BY (SELECT NULL);
                """;
            await using var cmd = new SqlCommand(sql, connection);
            var result = await cmd.ExecuteScalarAsync(ct);
            if (result is not null and not DBNull)
            {
                var systemIdle = Convert.ToInt32(result);
                return 100.0 - systemIdle;
            }
        }
        catch
        {
            // Unable to read CPU metrics
        }

        return 0;
    }

    private static async Task<long> GetMemoryUsedMbAsync(SqlConnection connection, CancellationToken ct)
    {
        try
        {
            const string sql = "SELECT physical_memory_in_use_kb / 1024 FROM sys.dm_os_process_memory;";
            await using var cmd = new SqlCommand(sql, connection);
            var result = await cmd.ExecuteScalarAsync(ct);
            if (result is not null and not DBNull)
                return Convert.ToInt64(result);
        }
        catch
        {
            // DMV not available (e.g. Azure SQL Database)
        }

        return 0;
    }

    private static async Task<double> GetAvgIoMbPerSecondAsync(SqlConnection connection, CancellationToken ct)
    {
        try
        {
            const string sql = """
                SELECT
                    SUM(num_of_bytes_read + num_of_bytes_written) / 1048576.0
                        / NULLIF(SUM(io_stall) / 1000.0, 0)
                FROM sys.dm_io_virtual_file_stats(DB_ID(), NULL);
                """;
            await using var cmd = new SqlCommand(sql, connection);
            var result = await cmd.ExecuteScalarAsync(ct);
            if (result is not null and not DBNull)
                return Math.Round(Convert.ToDouble(result), 2);
        }
        catch
        {
            // Unable to read IO metrics
        }

        return 0;
    }

    private static async Task<long> GetDatabaseSizeMbAsync(SqlConnection connection, CancellationToken ct)
    {
        try
        {
            const string sql = "SELECT SUM(size * 8 / 1024) FROM sys.database_files;";
            await using var cmd = new SqlCommand(sql, connection);
            var result = await cmd.ExecuteScalarAsync(ct);
            if (result is not null and not DBNull)
                return Convert.ToInt64(result);
        }
        catch
        {
            // Unable to read database size
        }

        return 0;
    }
}
