using Npgsql;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.PostgreSql;

public static class DataProfiler
{
    public static async Task<DataProfile> CollectAsync(NpgsqlConnection connection, CancellationToken ct = default)
    {
        var profile = new DataProfile();

        const string sql = @"
            SELECT 
                schemaname,
                relname,
                COALESCE(n_live_tup, 0) as row_count,
                COALESCE(pg_total_relation_size(quote_ident(schemaname) || '.' || quote_ident(relname)), 0) as total_size
            FROM pg_stat_user_tables
            ORDER BY pg_total_relation_size(quote_ident(schemaname) || '.' || quote_ident(relname)) DESC";

        await using var cmd = new NpgsqlCommand(sql, connection);
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
