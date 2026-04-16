using Npgsql;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.PostgreSql;

public class SchemaAnalyzer
{
    private readonly NpgsqlConnection _connection;

    public SchemaAnalyzer(NpgsqlConnection connection) => _connection = connection;

    public async Task<SchemaInventory> AnalyzeAsync(CancellationToken ct = default)
    {
        var inventory = new SchemaInventory();

        await CollectTablesAsync(inventory, ct);
        await CollectViewsAsync(inventory, ct);
        await CollectMaterializedViewsAsync(inventory, ct);
        await CollectFunctionsAsync(inventory, ct);
        await CollectIndexesAsync(inventory, ct);
        await CollectTriggersAsync(inventory, ct);
        await CollectConstraintsAsync(inventory, ct);
        await CollectSequencesAsync(inventory, ct);
        await CollectExtensionsAsync(inventory, ct);

        inventory.TableCount = inventory.Objects.Count(o => o.ObjectType == "Table");
        inventory.ViewCount = inventory.Objects.Count(o => o.ObjectType == "View" || o.ObjectType == "MaterializedView");
        inventory.StoredProcedureCount = inventory.Objects.Count(o => o.ObjectType == "Function");
        inventory.IndexCount = inventory.Objects.Count(o => o.ObjectType == "Index");
        inventory.TriggerCount = inventory.Objects.Count(o => o.ObjectType == "Trigger");

        return inventory;
    }

    internal async Task CollectTablesAsync(SchemaInventory inventory, CancellationToken ct)
    {
        const string sql = "SELECT schemaname, tablename FROM pg_tables WHERE schemaname NOT IN ('pg_catalog', 'information_schema')";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            inventory.Objects.Add(new SchemaObject
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                ObjectType = "Table"
            });
        }
    }

    internal async Task CollectViewsAsync(SchemaInventory inventory, CancellationToken ct)
    {
        const string sql = "SELECT schemaname, viewname FROM pg_views WHERE schemaname NOT IN ('pg_catalog', 'information_schema')";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            inventory.Objects.Add(new SchemaObject
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                ObjectType = "View"
            });
        }
    }

    internal async Task CollectMaterializedViewsAsync(SchemaInventory inventory, CancellationToken ct)
    {
        const string sql = "SELECT schemaname, matviewname FROM pg_matviews";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            inventory.Objects.Add(new SchemaObject
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                ObjectType = "MaterializedView"
            });
        }
    }

    internal async Task CollectFunctionsAsync(SchemaInventory inventory, CancellationToken ct)
    {
        const string sql = @"
            SELECT n.nspname, p.proname, CASE WHEN p.prokind = 'p' THEN 'Procedure' ELSE 'Function' END
            FROM pg_proc p
            JOIN pg_namespace n ON p.pronamespace = n.oid
            WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            inventory.Objects.Add(new SchemaObject
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                SubType = reader.GetString(2),
                ObjectType = "Function"
            });
        }
    }

    internal async Task CollectIndexesAsync(SchemaInventory inventory, CancellationToken ct)
    {
        const string sql = @"
            SELECT schemaname, indexname, tablename,
                   CASE WHEN indexdef LIKE '%UNIQUE%' THEN 'Unique' ELSE 'NonUnique' END
            FROM pg_indexes
            WHERE schemaname NOT IN ('pg_catalog', 'information_schema')";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            inventory.Objects.Add(new SchemaObject
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                ParentObjectName = reader.GetString(2),
                SubType = reader.GetString(3),
                ObjectType = "Index"
            });
        }
    }

    internal async Task CollectTriggersAsync(SchemaInventory inventory, CancellationToken ct)
    {
        const string sql = @"
            SELECT trigger_schema, trigger_name, event_object_table
            FROM information_schema.triggers
            WHERE trigger_schema NOT IN ('pg_catalog', 'information_schema')";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            inventory.Objects.Add(new SchemaObject
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                ParentObjectName = reader.GetString(2),
                ObjectType = "Trigger"
            });
        }
    }

    internal async Task CollectConstraintsAsync(SchemaInventory inventory, CancellationToken ct)
    {
        const string sql = @"
            SELECT constraint_schema, constraint_name, constraint_type, table_name
            FROM information_schema.table_constraints
            WHERE constraint_schema NOT IN ('pg_catalog', 'information_schema')";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            inventory.Objects.Add(new SchemaObject
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                SubType = reader.GetString(2),
                ParentObjectName = reader.GetString(3),
                ObjectType = "Constraint"
            });
        }
    }

    internal async Task CollectSequencesAsync(SchemaInventory inventory, CancellationToken ct)
    {
        const string sql = @"
            SELECT sequence_schema, sequence_name
            FROM information_schema.sequences
            WHERE sequence_schema NOT IN ('pg_catalog', 'information_schema')";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            inventory.Objects.Add(new SchemaObject
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                ObjectType = "Sequence"
            });
        }
    }

    internal async Task CollectExtensionsAsync(SchemaInventory inventory, CancellationToken ct)
    {
        const string sql = "SELECT extname FROM pg_extension WHERE extname != 'plpgsql'";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            inventory.Objects.Add(new SchemaObject
            {
                Schema = "public",
                Name = reader.GetString(0),
                ObjectType = "Extension"
            });
        }
    }
}
