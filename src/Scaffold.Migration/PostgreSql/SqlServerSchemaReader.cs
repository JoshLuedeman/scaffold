using Microsoft.Data.SqlClient;
using Scaffold.Migration.PostgreSql.Models;

namespace Scaffold.Migration.PostgreSql;

/// <summary>
/// Reads detailed schema information from a SQL Server source database
/// for DDL translation to PostgreSQL. Queries system views for columns,
/// constraints, indexes, and other metadata.
/// </summary>
public class SqlServerSchemaReader
{
    /// <summary>
    /// Reads all table definitions from the source SQL Server database.
    /// </summary>
    /// <param name="connectionString">SQL Server connection string.</param>
    /// <param name="includedTables">Optional filter: only include these table names (schema.table or just table).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of fully-populated table definitions.</returns>
    public virtual async Task<List<TableDefinition>> ReadSchemaAsync(
        string connectionString,
        IReadOnlyList<string>? includedTables = null,
        CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var tables = await ReadTablesAsync(connection, includedTables, ct);
        var tableMap = tables.ToDictionary(t => $"{t.Schema}.{t.TableName}", StringComparer.OrdinalIgnoreCase);

        await ReadColumnsAsync(connection, tableMap, ct);
        await ReadPrimaryKeysAsync(connection, tableMap, ct);
        await ReadForeignKeysAsync(connection, tableMap, ct);
        await ReadIndexesAsync(connection, tableMap, ct);
        await ReadCheckConstraintsAsync(connection, tableMap, ct);
        await ReadUniqueConstraintsAsync(connection, tableMap, ct);

        return tables;
    }

    internal static async Task<List<TableDefinition>> ReadTablesAsync(
        SqlConnection connection, IReadOnlyList<string>? includedTables, CancellationToken ct)
    {
        const string sql = """
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_SCHEMA, TABLE_NAME
            """;

        var tables = new List<TableDefinition>();
        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var schema = reader.GetString(0);
            var name = reader.GetString(1);

            if (includedTables is { Count: > 0 } && !IsTableIncluded(schema, name, includedTables))
                continue;

            tables.Add(new TableDefinition { Schema = schema, TableName = name });
        }

        return tables;
    }

    internal static async Task ReadColumnsAsync(
        SqlConnection connection, Dictionary<string, TableDefinition> tableMap, CancellationToken ct)
    {
        const string sql = """
            SELECT
                c.TABLE_SCHEMA,
                c.TABLE_NAME,
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.NUMERIC_PRECISION,
                c.NUMERIC_SCALE,
                c.IS_NULLABLE,
                c.COLUMN_DEFAULT,
                c.ORDINAL_POSITION,
                COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') AS IsIdentity,
                COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsComputed') AS IsComputed,
                cc.definition AS ComputedExpression,
                c.DATETIME_PRECISION
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN sys.computed_columns cc
                ON cc.object_id = OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME)
                AND cc.name = c.COLUMN_NAME
            ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION
            """;

        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var key = $"{reader.GetString(0)}.{reader.GetString(1)}";
            if (!tableMap.TryGetValue(key, out var table)) continue;

            var col = new ColumnDefinition
            {
                Name = reader.GetString(2),
                DataType = reader.GetString(3),
                MaxLength = reader.IsDBNull(4) ? null : Convert.ToInt32(reader.GetValue(4)),
                Precision = reader.IsDBNull(5) ? null : (int)reader.GetByte(5),
                Scale = reader.IsDBNull(6) ? null : (int)reader.GetInt32(6),
                IsNullable = reader.GetString(7) == "YES",
                DefaultExpression = reader.IsDBNull(8) ? null : reader.GetString(8),
                OrdinalPosition = reader.GetInt32(9),
                IsIdentity = !reader.IsDBNull(10) && reader.GetInt32(10) == 1,
                IsComputed = !reader.IsDBNull(11) && reader.GetInt32(11) == 1,
                ComputedExpression = reader.IsDBNull(12) ? null : reader.GetString(12)
            };

            // Use DATETIME_PRECISION for datetime-family types instead of NUMERIC_PRECISION
            var dataType = col.DataType.ToLowerInvariant();
            if (dataType is "datetime2" or "datetimeoffset" or "time" && !reader.IsDBNull(13))
            {
                col.Precision = Convert.ToInt32(reader.GetValue(13));
            }
            table.Columns.Add(col);
        }
    }

    internal static async Task ReadPrimaryKeysAsync(
        SqlConnection connection, Dictionary<string, TableDefinition> tableMap, CancellationToken ct)
    {
        const string sql = """
            SELECT
                s.name AS SchemaName,
                t.name AS TableName,
                kc.name AS ConstraintName,
                i.type_desc AS IndexType,
                c.name AS ColumnName,
                ic.key_ordinal
            FROM sys.key_constraints kc
            INNER JOIN sys.tables t ON kc.parent_object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            INNER JOIN sys.indexes i ON i.object_id = t.object_id AND i.name = kc.name
            INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE kc.type = 'PK'
            ORDER BY s.name, t.name, ic.key_ordinal
            """;

        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var key = $"{reader.GetString(0)}.{reader.GetString(1)}";
            if (!tableMap.TryGetValue(key, out var table)) continue;

            table.PrimaryKey ??= new PrimaryKeyDefinition
            {
                Name = reader.GetString(2),
                IsClustered = reader.GetString(3).Contains("CLUSTERED", StringComparison.OrdinalIgnoreCase)
                              && !reader.GetString(3).Contains("NONCLUSTERED", StringComparison.OrdinalIgnoreCase)
            };
            table.PrimaryKey.Columns.Add(reader.GetString(4));
        }
    }

    internal static async Task ReadForeignKeysAsync(
        SqlConnection connection, Dictionary<string, TableDefinition> tableMap, CancellationToken ct)
    {
        const string sql = """
            SELECT
                s.name AS SchemaName,
                t.name AS TableName,
                fk.name AS FKName,
                rs.name AS RefSchemaName,
                rt.name AS RefTableName,
                c.name AS ColumnName,
                rc.name AS RefColumnName,
                fk.delete_referential_action_desc,
                fk.update_referential_action_desc,
                fkc.constraint_column_id
            FROM sys.foreign_keys fk
            INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            INNER JOIN sys.tables rt ON fk.referenced_object_id = rt.object_id
            INNER JOIN sys.schemas rs ON rt.schema_id = rs.schema_id
            INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
            INNER JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
            ORDER BY s.name, t.name, fk.name, fkc.constraint_column_id
            """;

        var fkMap = new Dictionary<string, ForeignKeyDefinition>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var key = $"{reader.GetString(0)}.{reader.GetString(1)}";
            if (!tableMap.TryGetValue(key, out var table)) continue;

            var fkName = reader.GetString(2);
            var fkKey = $"{key}.{fkName}";

            if (!fkMap.TryGetValue(fkKey, out var fk))
            {
                fk = new ForeignKeyDefinition
                {
                    Name = fkName,
                    ReferencedSchema = reader.GetString(3),
                    ReferencedTable = reader.GetString(4),
                    DeleteAction = reader.GetString(7).Replace("_", " "),
                    UpdateAction = reader.GetString(8).Replace("_", " ")
                };
                fkMap[fkKey] = fk;
                table.ForeignKeys.Add(fk);
            }

            fk.Columns.Add(reader.GetString(5));
            fk.ReferencedColumns.Add(reader.GetString(6));
        }
    }

    internal static async Task ReadIndexesAsync(
        SqlConnection connection, Dictionary<string, TableDefinition> tableMap, CancellationToken ct)
    {
        const string sql = """
            SELECT
                s.name AS SchemaName,
                t.name AS TableName,
                i.name AS IndexName,
                i.is_unique,
                i.type_desc,
                i.filter_definition,
                c.name AS ColumnName,
                ic.is_included_column,
                ic.key_ordinal
            FROM sys.indexes i
            INNER JOIN sys.tables t ON i.object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.is_primary_key = 0
              AND i.is_unique_constraint = 0
              AND i.type > 0
              AND i.name IS NOT NULL
            ORDER BY s.name, t.name, i.name, ic.is_included_column, ic.key_ordinal
            """;

        var idxMap = new Dictionary<string, IndexDefinition>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var key = $"{reader.GetString(0)}.{reader.GetString(1)}";
            if (!tableMap.TryGetValue(key, out var table)) continue;

            var idxName = reader.GetString(2);
            var idxKey = $"{key}.{idxName}";

            if (!idxMap.TryGetValue(idxKey, out var idx))
            {
                idx = new IndexDefinition
                {
                    Name = idxName,
                    IsUnique = reader.GetBoolean(3),
                    IsClustered = reader.GetString(4).Contains("CLUSTERED", StringComparison.OrdinalIgnoreCase)
                                  && !reader.GetString(4).Contains("NONCLUSTERED", StringComparison.OrdinalIgnoreCase),
                    FilterExpression = reader.IsDBNull(5) ? null : reader.GetString(5)
                };
                idxMap[idxKey] = idx;
                table.Indexes.Add(idx);
            }

            var isIncluded = reader.GetBoolean(7);
            var colName = reader.GetString(6);
            if (isIncluded)
                idx.IncludedColumns.Add(colName);
            else
                idx.Columns.Add(colName);
        }
    }

    internal static async Task ReadCheckConstraintsAsync(
        SqlConnection connection, Dictionary<string, TableDefinition> tableMap, CancellationToken ct)
    {
        const string sql = """
            SELECT
                s.name AS SchemaName,
                t.name AS TableName,
                cc.name AS ConstraintName,
                cc.definition
            FROM sys.check_constraints cc
            INNER JOIN sys.tables t ON cc.parent_object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            ORDER BY s.name, t.name, cc.name
            """;

        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var key = $"{reader.GetString(0)}.{reader.GetString(1)}";
            if (!tableMap.TryGetValue(key, out var table)) continue;

            table.CheckConstraints.Add(new CheckConstraintDefinition
            {
                Name = reader.GetString(2),
                Expression = reader.GetString(3)
            });
        }
    }

    internal static async Task ReadUniqueConstraintsAsync(
        SqlConnection connection, Dictionary<string, TableDefinition> tableMap, CancellationToken ct)
    {
        const string sql = """
            SELECT
                s.name AS SchemaName,
                t.name AS TableName,
                kc.name AS ConstraintName,
                c.name AS ColumnName,
                ic.key_ordinal
            FROM sys.key_constraints kc
            INNER JOIN sys.tables t ON kc.parent_object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            INNER JOIN sys.indexes i ON i.object_id = t.object_id AND i.name = kc.name
            INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE kc.type = 'UQ'
            ORDER BY s.name, t.name, kc.name, ic.key_ordinal
            """;

        var uqMap = new Dictionary<string, UniqueConstraintDefinition>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var key = $"{reader.GetString(0)}.{reader.GetString(1)}";
            if (!tableMap.TryGetValue(key, out var table)) continue;

            var uqName = reader.GetString(2);
            var uqKey = $"{key}.{uqName}";

            if (!uqMap.TryGetValue(uqKey, out var uq))
            {
                uq = new UniqueConstraintDefinition { Name = uqName };
                uqMap[uqKey] = uq;
                table.UniqueConstraints.Add(uq);
            }

            uq.Columns.Add(reader.GetString(3));
        }
    }

    /// <summary>
    /// Checks whether a table is in the inclusion list.
    /// Supports both "schema.table" and plain "table" formats.
    /// </summary>
    internal static bool IsTableIncluded(string schema, string tableName, IReadOnlyList<string> includedTables)
    {
        foreach (var included in includedTables)
        {
            if (included.Contains('.'))
            {
                if (string.Equals(included, $"{schema}.{tableName}", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else
            {
                if (string.Equals(included, tableName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }
}