using System.Text;
using System.Text.RegularExpressions;
using Scaffold.Migration.PostgreSql.Models;

namespace Scaffold.Migration.PostgreSql;

/// <summary>
/// Translates SQL Server table definitions to PostgreSQL DDL statements.
/// Uses <see cref="DataTypeMapper"/> for type conversion and produces
/// CREATE TABLE, ALTER TABLE (FK), and CREATE INDEX statements.
/// </summary>
public class DdlTranslator
{
    /// <summary>
    /// Generates all PostgreSQL DDL for the given table definitions, in dependency order.
    /// Returns: CREATE TABLE statements, then ALTER TABLE for FKs, then CREATE INDEX.
    /// </summary>
    public virtual List<string> TranslateSchema(IReadOnlyList<TableDefinition> tables)
    {
        var ddl = new List<string>();
        var ordered = TopologicalSort(tables);

        foreach (var table in ordered)
        {
            ddl.Add(GenerateCreateTable(table));
        }

        // FKs as separate ALTER TABLEs (after all tables created)
        foreach (var table in ordered)
        {
            foreach (var fk in table.ForeignKeys)
            {
                ddl.Add(GenerateAddForeignKey(table, fk));
            }
        }

        // Indexes
        foreach (var table in ordered)
        {
            foreach (var idx in table.Indexes)
            {
                ddl.Add(GenerateCreateIndex(table, idx));
            }
        }

        return ddl;
    }

    /// <summary>
    /// Generates a single CREATE TABLE statement for the given table definition.
    /// </summary>
    public string GenerateCreateTable(TableDefinition table)
    {
        var sb = new StringBuilder();
        var pgSchema = MapSchema(table.Schema);

        sb.AppendLine($"CREATE TABLE {QuoteIdentifier(pgSchema)}.{QuoteIdentifier(table.TableName)} (");

        var columnDefs = new List<string>();

        foreach (var col in table.Columns.OrderBy(c => c.OrdinalPosition))
        {
            if (col.IsComputed)
            {
                var sanitizedExpr = (col.ComputedExpression ?? "unknown").Replace("\r", " ").Replace("\n", " ");
                columnDefs.Add($"    -- COMPUTED COLUMN: {QuoteIdentifier(col.Name)} = {sanitizedExpr} (requires manual translation)");
                continue;
            }

            var colDef = new StringBuilder();
            colDef.Append($"    {QuoteIdentifier(col.Name)} ");

            if (col.IsIdentity)
            {
                colDef.Append(DataTypeMapper.MapIdentity(col.DataType));
            }
            else
            {
                colDef.Append(DataTypeMapper.MapType(col.DataType, col.MaxLength, col.Precision, col.Scale));
            }

            if (!col.IsNullable)
            {
                colDef.Append(" NOT NULL");
            }

            if (!col.IsIdentity && col.DefaultExpression != null)
            {
                var pgDefault = DataTypeMapper.MapDefaultExpression(col.DefaultExpression);
                if (pgDefault != null)
                {
                    // BIT defaults (0/1) must become boolean literals for PG
                    if (col.DataType.Equals("bit", StringComparison.OrdinalIgnoreCase))
                    {
                        pgDefault = pgDefault switch
                        {
                            "1" => "true",
                            "0" => "false",
                            _ => pgDefault
                        };
                    }

                    colDef.Append($" DEFAULT {pgDefault}");
                }
            }

            // Add type mapping warnings as inline comments
            if (DataTypeMapper.HasWarning(col.DataType, out var warning))
            {
                colDef.Append($" /* WARNING: {warning} */");
            }

            columnDefs.Add(colDef.ToString());
        }

        // Primary key constraint
        if (table.PrimaryKey is { Columns.Count: > 0 })
        {
            var pkCols = string.Join(", ", table.PrimaryKey.Columns.Select(QuoteIdentifier));
            columnDefs.Add($"    CONSTRAINT {QuoteIdentifier(table.PrimaryKey.Name)} PRIMARY KEY ({pkCols})");
        }

        // Unique constraints
        foreach (var uq in table.UniqueConstraints)
        {
            var uqCols = string.Join(", ", uq.Columns.Select(QuoteIdentifier));
            columnDefs.Add($"    CONSTRAINT {QuoteIdentifier(uq.Name)} UNIQUE ({uqCols})");
        }

        // Check constraints
        foreach (var chk in table.CheckConstraints)
        {
            var pgExpr = TranslateCheckExpression(chk.Expression);
            columnDefs.Add($"    CONSTRAINT {QuoteIdentifier(chk.Name)} CHECK ({pgExpr})");
        }

        sb.AppendLine(string.Join(",\n", columnDefs));
        sb.Append(");");

        return sb.ToString();
    }

    /// <summary>
    /// Generates an ALTER TABLE ... ADD CONSTRAINT ... FOREIGN KEY statement.
    /// </summary>
    public string GenerateAddForeignKey(TableDefinition table, ForeignKeyDefinition fk)
    {
        var pgSchema = MapSchema(table.Schema);
        var pgRefSchema = MapSchema(fk.ReferencedSchema);
        var fkCols = string.Join(", ", fk.Columns.Select(QuoteIdentifier));
        var refCols = string.Join(", ", fk.ReferencedColumns.Select(QuoteIdentifier));

        var sb = new StringBuilder();
        sb.Append($"ALTER TABLE {QuoteIdentifier(pgSchema)}.{QuoteIdentifier(table.TableName)} ");
        sb.AppendLine($"ADD CONSTRAINT {QuoteIdentifier(fk.Name)}");
        sb.Append($"    FOREIGN KEY ({fkCols}) REFERENCES {QuoteIdentifier(pgRefSchema)}.{QuoteIdentifier(fk.ReferencedTable)} ({refCols})");
        sb.Append($" ON DELETE {ValidateReferentialAction(fk.DeleteAction)} ON UPDATE {ValidateReferentialAction(fk.UpdateAction)};");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a CREATE INDEX statement for the given index definition.
    /// </summary>
    public string GenerateCreateIndex(TableDefinition table, IndexDefinition index)
    {
        var pgSchema = MapSchema(table.Schema);
        var keyCols = string.Join(", ", index.Columns.Select(QuoteIdentifier));

        var sb = new StringBuilder();
        sb.Append("CREATE ");
        if (index.IsUnique) sb.Append("UNIQUE ");
        sb.Append($"INDEX {QuoteIdentifier(index.Name)} ON {QuoteIdentifier(pgSchema)}.{QuoteIdentifier(table.TableName)} ({keyCols})");

        if (index.IncludedColumns.Count > 0)
        {
            var inclCols = string.Join(", ", index.IncludedColumns.Select(QuoteIdentifier));
            sb.Append($" INCLUDE ({inclCols})");
        }

        if (!string.IsNullOrWhiteSpace(index.FilterExpression))
        {
            var pgFilter = TranslateFilterExpression(index.FilterExpression);
            sb.Append($" WHERE {pgFilter}");
        }

        sb.Append(';');
        return sb.ToString();
    }

    /// <summary>
    /// Maps a SQL Server schema name to its PostgreSQL equivalent.
    /// "dbo" becomes "public"; all others are preserved.
    /// </summary>
    public static string MapSchema(string schema)
        => schema.Equals("dbo", StringComparison.OrdinalIgnoreCase) ? "public" : schema;

    /// <summary>
    /// Wraps an identifier in PostgreSQL double-quotes.
    /// </summary>
    public static string QuoteIdentifier(string name) => $"\"{name.Replace("\"", "\"\"")}\"";

    private static readonly HashSet<string> ValidReferentialActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "NO ACTION", "CASCADE", "SET NULL", "SET DEFAULT", "RESTRICT"
    };

    private static string ValidateReferentialAction(string action)
    {
        if (ValidReferentialActions.Contains(action))
            return action.ToUpperInvariant();
        return "NO ACTION"; // safe default
    }


    /// <summary>
    /// Translates a T-SQL CHECK constraint expression to PostgreSQL syntax.
    /// Handles common patterns like bracket-quoted identifiers and T-SQL functions.
    /// </summary>
    internal static string TranslateCheckExpression(string expression)
    {
        var result = expression;

        // Remove outer parentheses
        while (result.StartsWith('(') && result.EndsWith(')') && result.Length > 2)
        {
            // Only strip if balanced
            var inner = result[1..^1];
            var depth = 0;
            var balanced = true;
            foreach (var ch in inner)
            {
                if (ch == '(') depth++;
                else if (ch == ')') depth--;
                if (depth < 0) { balanced = false; break; }
            }
            if (balanced && depth == 0)
                result = inner;
            else
                break;
        }

        // Replace [column] with "column" (using QuoteIdentifier to escape embedded double quotes)
        result = Regex.Replace(result, @"\[([^\]]+)\]", m => QuoteIdentifier(m.Groups[1].Value));

        return result;
    }

    /// <summary>
    /// Translates a T-SQL filter expression (for filtered indexes) to PostgreSQL syntax.
    /// </summary>
    internal static string TranslateFilterExpression(string expression)
    {
        var result = expression;

        // Remove outer parentheses
        while (result.StartsWith('(') && result.EndsWith(')') && result.Length > 2)
        {
            var inner = result[1..^1];
            var depth = 0;
            var balanced = true;
            foreach (var ch in inner)
            {
                if (ch == '(') depth++;
                else if (ch == ')') depth--;
                if (depth < 0) { balanced = false; break; }
            }
            if (balanced && depth == 0)
                result = inner;
            else
                break;
        }

        // Replace [column] with "column" (using QuoteIdentifier to escape embedded double quotes)
        result = Regex.Replace(result, @"\[([^\]]+)\]", m => QuoteIdentifier(m.Groups[1].Value));

        return result;
    }

    /// <summary>
    /// Sorts tables in dependency order so that referenced tables come before
    /// tables that reference them via foreign keys.
    /// </summary>
    internal static List<TableDefinition> TopologicalSort(IReadOnlyList<TableDefinition> tables)
    {
        var tableKeys = new HashSet<string>(
            tables.Select(t => $"{t.Schema}.{t.TableName}"),
            StringComparer.OrdinalIgnoreCase);

        // Build adjacency: table → set of tables it depends on
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var tableByKey = new Dictionary<string, TableDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in tables)
        {
            var key = $"{t.Schema}.{t.TableName}";
            tableByKey[key] = t;
            deps[key] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var fk in t.ForeignKeys)
            {
                var refKey = $"{fk.ReferencedSchema}.{fk.ReferencedTable}";
                // Only add dependency if the referenced table is in our set
                // and it's not a self-reference
                if (tableKeys.Contains(refKey) && !string.Equals(refKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    deps[key].Add(refKey);
                }
            }
        }

        // Kahn's algorithm
        var result = new List<TableDefinition>();
        var noDeps = new Queue<string>(deps.Where(d => d.Value.Count == 0).Select(d => d.Key));
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (noDeps.Count > 0)
        {
            var key = noDeps.Dequeue();
            if (!visited.Add(key)) continue;

            result.Add(tableByKey[key]);

            foreach (var (otherKey, otherDeps) in deps)
            {
                if (visited.Contains(otherKey)) continue;
                otherDeps.Remove(key);
                if (otherDeps.Count == 0)
                    noDeps.Enqueue(otherKey);
            }
        }

        // Add any remaining (circular dependencies) in original order
        foreach (var t in tables)
        {
            var key = $"{t.Schema}.{t.TableName}";
            if (!visited.Contains(key))
                result.Add(t);
        }

        return result;
    }
}