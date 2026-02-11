using Microsoft.Data.SqlClient;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.SqlServer;

public static class DataProfiler
{
    public static async Task<DataProfile> CollectAsync(SqlConnection connection, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                s.name       AS SchemaName,
                t.name       AS TableName,
                p.rows       AS RowCount,
                SUM(a.total_pages) * 8 * 1024 AS SizeBytes
            FROM sys.tables t
            JOIN sys.schemas s          ON t.schema_id  = s.schema_id
            JOIN sys.indexes i          ON t.object_id  = i.object_id
            JOIN sys.partitions p       ON i.object_id  = p.object_id AND i.index_id = p.index_id
            JOIN sys.allocation_units a ON p.partition_id = a.container_id
            WHERE i.index_id <= 1
            GROUP BY s.name, t.name, p.rows
            ORDER BY SizeBytes DESC;
            """;

        var profile = new DataProfile();

        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var table = new TableProfile
            {
                SchemaName = reader.GetString(0),
                TableName = reader.GetString(1),
                RowCount = reader.GetInt64(2),
                SizeBytes = reader.GetInt64(3)
            };

            profile.Tables.Add(table);
            profile.TotalRowCount += table.RowCount;
            profile.TotalSizeBytes += table.SizeBytes;
        }

        return profile;
    }
}
