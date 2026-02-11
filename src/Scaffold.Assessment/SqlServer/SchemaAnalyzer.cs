using Microsoft.Data.SqlClient;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.SqlServer;

public class SchemaAnalyzer
{
    private readonly SqlConnection _connection;

    public SchemaAnalyzer(SqlConnection connection)
    {
        _connection = connection;
    }

    public async Task<SchemaInventory> AnalyzeAsync(CancellationToken ct = default)
    {
        var inventory = new SchemaInventory();

        await CollectTablesAsync(inventory, ct);
        await CollectViewsAsync(inventory, ct);
        await CollectStoredProceduresAsync(inventory, ct);
        await CollectFunctionsAsync(inventory, ct);
        await CollectIndexesAsync(inventory, ct);
        await CollectTriggersAsync(inventory, ct);
        await CollectConstraintsAsync(inventory, ct);

        inventory.TableCount = inventory.Objects.Count(o => o.ObjectType == "Table");
        inventory.ViewCount = inventory.Objects.Count(o => o.ObjectType == "View");
        inventory.StoredProcedureCount = inventory.Objects.Count(o => o.ObjectType == "StoredProcedure");
        inventory.IndexCount = inventory.Objects.Count(o => o.ObjectType == "Index");
        inventory.TriggerCount = inventory.Objects.Count(o => o.ObjectType == "Trigger");

        return inventory;
    }

    private async Task CollectTablesAsync(SchemaInventory inventory, CancellationToken ct)
    {
        const string sql = "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
        await using var cmd = new SqlCommand(sql, _connection);
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

    private async Task CollectViewsAsync(SchemaInventory inventory, CancellationToken ct)
    {
        const string sql = "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.VIEWS";
        await using var cmd = new SqlCommand(sql, _connection);
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

    private async Task CollectStoredProceduresAsync(SchemaInventory inventory, CancellationToken ct)
    {
        const string sql = "SELECT SCHEMA_NAME(schema_id), name FROM sys.procedures";
        await using var cmd = new SqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            inventory.Objects.Add(new SchemaObject
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                ObjectType = "StoredProcedure"
            });
        }
    }

    private async Task CollectFunctionsAsync(SchemaInventory inventory, CancellationToken ct)
    {
        const string sql = "SELECT SCHEMA_NAME(schema_id), name FROM sys.objects WHERE type IN ('FN', 'IF', 'TF')";
        await using var cmd = new SqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            inventory.Objects.Add(new SchemaObject
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                ObjectType = "Function"
            });
        }
    }

    private async Task CollectIndexesAsync(SchemaInventory inventory, CancellationToken ct)
    {
        const string sql = @"
            SELECT SCHEMA_NAME(o.schema_id), i.name
            FROM sys.indexes i
            INNER JOIN sys.objects o ON i.object_id = o.object_id
            WHERE i.name IS NOT NULL AND o.is_ms_shipped = 0";
        await using var cmd = new SqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            inventory.Objects.Add(new SchemaObject
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                ObjectType = "Index"
            });
        }
    }

    private async Task CollectTriggersAsync(SchemaInventory inventory, CancellationToken ct)
    {
        const string sql = @"
            SELECT SCHEMA_NAME(o.schema_id), t.name
            FROM sys.triggers t
            INNER JOIN sys.objects o ON t.parent_id = o.object_id
            WHERE t.parent_id <> 0";
        await using var cmd = new SqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            inventory.Objects.Add(new SchemaObject
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                ObjectType = "Trigger"
            });
        }
    }

    private async Task CollectConstraintsAsync(SchemaInventory inventory, CancellationToken ct)
    {
        const string sql = "SELECT CONSTRAINT_SCHEMA, CONSTRAINT_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS";
        await using var cmd = new SqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            inventory.Objects.Add(new SchemaObject
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                ObjectType = "Constraint"
            });
        }
    }
}
