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
            SELECT SCHEMA_NAME(o.schema_id), i.name, o.name, i.type_desc
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
                ParentObjectName = reader.GetString(2),
                SubType = reader.GetString(3),
                ObjectType = "Index"
            });
        }
    }

    private async Task CollectTriggersAsync(SchemaInventory inventory, CancellationToken ct)
    {
        const string sql = @"
            SELECT SCHEMA_NAME(o.schema_id), t.name, o.name
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
                ParentObjectName = reader.GetString(2),
                ObjectType = "Trigger"
            });
        }
    }

    private async Task CollectConstraintsAsync(SchemaInventory inventory, CancellationToken ct)
    {
        // Collect non-FK constraints (PK, UNIQUE, CHECK)
        const string nonFkSql = @"
            SELECT c.CONSTRAINT_SCHEMA, c.CONSTRAINT_NAME, c.CONSTRAINT_TYPE, c.TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS c
            WHERE c.CONSTRAINT_TYPE <> 'FOREIGN KEY'";
        await using var cmd = new SqlCommand(nonFkSql, _connection);
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
        await reader.CloseAsync();

        // Collect FK constraints with full relationship metadata
        const string fkSql = @"
            SELECT
                SCHEMA_NAME(fk.schema_id)          AS FkSchema,
                fk.name                             AS FkName,
                OBJECT_NAME(fk.parent_object_id)    AS ParentTable,
                SCHEMA_NAME(rt.schema_id)           AS ReferencedSchema,
                rt.name                             AS ReferencedTable,
                STRING_AGG(COL_NAME(fkc.parent_object_id, fkc.parent_column_id), ',')
                    WITHIN GROUP (ORDER BY fkc.constraint_column_id)
                                                    AS FkColumns,
                STRING_AGG(COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id), ',')
                    WITHIN GROUP (ORDER BY fkc.constraint_column_id)
                                                    AS ReferencedColumns,
                fk.delete_referential_action_desc   AS DeleteAction,
                fk.update_referential_action_desc   AS UpdateAction
            FROM sys.foreign_keys fk
            JOIN sys.tables rt ON fk.referenced_object_id = rt.object_id
            JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            GROUP BY fk.schema_id, fk.name, fk.parent_object_id, rt.schema_id, rt.name,
                     fk.delete_referential_action_desc, fk.update_referential_action_desc";
        await using var fkCmd = new SqlCommand(fkSql, _connection);
        await using var fkReader = await fkCmd.ExecuteReaderAsync(ct);
        while (await fkReader.ReadAsync(ct))
        {
            inventory.Objects.Add(new SchemaObject
            {
                Schema = fkReader.GetString(0),
                Name = fkReader.GetString(1),
                ParentObjectName = fkReader.GetString(2),
                ReferencedSchema = fkReader.GetString(3),
                ReferencedTable = fkReader.GetString(4),
                Columns = fkReader.IsDBNull(5) ? null : fkReader.GetString(5),
                ReferencedColumns = fkReader.IsDBNull(6) ? null : fkReader.GetString(6),
                DeleteAction = fkReader.GetString(7),
                UpdateAction = fkReader.GetString(8),
                SubType = "FOREIGN KEY",
                ObjectType = "Constraint"
            });
        }
    }
}
