using System.Text;
using Scaffold.Migration.PostgreSql.Models;
using Scaffold.Migration.Shared;

namespace Scaffold.Migration.PostgreSql;

/// <summary>
/// Generates DDL from PG-native models to deploy on a PostgreSQL target.
/// Produces statements in dependency order: enum types → sequences → tables (topological)
/// → foreign keys → indexes → views → functions.
/// </summary>
public class PostgreSqlDdlGenerator
{
    /// <summary>
    /// Generates all DDL statements from a PG schema snapshot, in dependency order.
    /// </summary>
    public virtual List<string> GenerateDdl(PgSchemaSnapshot schema)
    {
        var ddl = new List<string>();

        // 1. Enum types (must exist before tables that reference them)
        foreach (var enumType in schema.EnumTypes)
        {
            ddl.Add(GenerateCreateEnum(enumType));
        }

        // 2. Sequences (must exist before tables with default nextval())
        foreach (var sequence in schema.Sequences)
        {
            ddl.Add(GenerateCreateSequence(sequence));
        }

        // 3. Tables in topological order (parents before children)
        var orderedTables = TopologicalSorter.Sort(
            schema.Tables,
            t => t.QualifiedName,
            t => t.ForeignKeys.Select(fk => $"{fk.ReferencedSchema}.{fk.ReferencedTable}"));

        foreach (var table in orderedTables)
        {
            ddl.Add(GenerateCreateTable(table));
        }

        // 4. Foreign keys (after all tables exist)
        foreach (var table in orderedTables)
        {
            foreach (var fk in table.ForeignKeys)
            {
                ddl.Add(GenerateAddForeignKey(table, fk));
            }
        }

        // 5. Indexes
        foreach (var table in orderedTables)
        {
            foreach (var index in table.Indexes)
            {
                ddl.Add(GenerateCreateIndex(table, index));
            }
        }

        // 6. Views
        foreach (var view in schema.Views)
        {
            ddl.Add(GenerateCreateView(view));
        }

        // 7. Functions
        foreach (var function in schema.Functions)
        {
            ddl.Add(GenerateCreateFunction(function));
        }

        return ddl;
    }

    internal string GenerateCreateEnum(PgEnumTypeDefinition enumType)
    {
        var labels = string.Join(", ", enumType.Labels.Select(l => $"'{l.Replace("'", "''")}'"));
        return $"CREATE TYPE {Quote(enumType.Schema)}.{Quote(enumType.Name)} AS ENUM ({labels});";
    }

    internal string GenerateCreateSequence(PgSequenceDefinition sequence)
    {
        var sb = new StringBuilder();
        sb.Append($"CREATE SEQUENCE {Quote(sequence.Schema)}.{Quote(sequence.Name)}");
        sb.Append($" AS {sequence.DataType}");
        sb.Append($" START WITH {sequence.StartValue}");
        sb.Append($" INCREMENT BY {sequence.IncrementBy}");

        if (sequence.MinValue.HasValue)
            sb.Append($" MINVALUE {sequence.MinValue.Value}");
        else
            sb.Append(" NO MINVALUE");

        if (sequence.MaxValue.HasValue)
            sb.Append($" MAXVALUE {sequence.MaxValue.Value}");
        else
            sb.Append(" NO MAXVALUE");

        sb.Append(sequence.IsCyclic ? " CYCLE" : " NO CYCLE");

        if (!string.IsNullOrEmpty(sequence.OwnedBy))
        {
            // OwnedBy is "schema.table.column" — need to quote each part
            var parts = sequence.OwnedBy.Split('.');
            if (parts.Length == 3)
            {
                sb.Append($" OWNED BY {Quote(parts[0])}.{Quote(parts[1])}.{Quote(parts[2])}");
            }
            else
            {
                // Fallback: quote the whole thing
                sb.Append($" OWNED BY {sequence.OwnedBy}");
            }
        }

        sb.Append(';');
        return sb.ToString();
    }

    internal string GenerateCreateTable(PgTableDefinition table)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE {Quote(table.Schema)}.{Quote(table.TableName)} (");

        var parts = new List<string>();

        foreach (var col in table.Columns.OrderBy(c => c.OrdinalPosition))
        {
            parts.Add(GenerateColumnDefinition(col));
        }

        // Primary key constraint
        if (table.PrimaryKey is { Columns.Count: > 0 })
        {
            var pkCols = string.Join(", ", table.PrimaryKey.Columns.Select(Quote));
            parts.Add($"    CONSTRAINT {Quote(table.PrimaryKey.Name)} PRIMARY KEY ({pkCols})");
        }

        // Unique constraints
        foreach (var uq in table.UniqueConstraints)
        {
            var uqCols = string.Join(", ", uq.Columns.Select(Quote));
            parts.Add($"    CONSTRAINT {Quote(uq.Name)} UNIQUE ({uqCols})");
        }

        // Check constraints
        foreach (var chk in table.CheckConstraints)
        {
            parts.Add($"    CONSTRAINT {Quote(chk.Name)} CHECK ({chk.Expression})");
        }

        sb.AppendLine(string.Join(",\n", parts));
        sb.Append(");");

        return sb.ToString();
    }

    private static string GenerateColumnDefinition(PgColumnDefinition col)
    {
        var sb = new StringBuilder();
        sb.Append($"    {Quote(col.Name)} ");

        if (col.IsIdentity)
        {
            // Use GENERATED ... AS IDENTITY
            sb.Append(col.FullType);
            sb.Append($" GENERATED {col.IdentityGeneration ?? "BY DEFAULT"} AS IDENTITY");
        }
        else if (col.IsGenerated && !string.IsNullOrEmpty(col.GenerationExpression))
        {
            sb.Append(col.FullType);
            sb.Append($" GENERATED ALWAYS AS ({col.GenerationExpression}) STORED");
        }
        else
        {
            sb.Append(col.FullType);

            if (col.DefaultExpression != null)
            {
                sb.Append($" DEFAULT {col.DefaultExpression}");
            }
        }

        if (!col.IsNullable)
        {
            sb.Append(" NOT NULL");
        }

        if (!string.IsNullOrEmpty(col.Collation))
        {
            sb.Append($" COLLATE {Quote(col.Collation)}");
        }

        return sb.ToString();
    }

    internal string GenerateAddForeignKey(PgTableDefinition table, PgForeignKeyDefinition fk)
    {
        var fkCols = string.Join(", ", fk.Columns.Select(Quote));
        var refCols = string.Join(", ", fk.ReferencedColumns.Select(Quote));

        var sb = new StringBuilder();
        sb.Append($"ALTER TABLE {Quote(table.Schema)}.{Quote(table.TableName)} ");
        sb.AppendLine($"ADD CONSTRAINT {Quote(fk.Name)}");
        sb.Append($"    FOREIGN KEY ({fkCols}) REFERENCES {Quote(fk.ReferencedSchema)}.{Quote(fk.ReferencedTable)} ({refCols})");
        sb.Append($" ON DELETE {ValidateReferentialAction(fk.DeleteAction)} ON UPDATE {ValidateReferentialAction(fk.UpdateAction)};");

        return sb.ToString();
    }

    internal string GenerateCreateIndex(PgTableDefinition table, PgIndexDefinition index)
    {
        // If raw DDL is available and appears to be a complete statement, use it directly
        if (!string.IsNullOrWhiteSpace(index.RawDdl) &&
            index.RawDdl.StartsWith("CREATE", StringComparison.OrdinalIgnoreCase))
        {
            var rawDdl = index.RawDdl.TrimEnd();
            return rawDdl.EndsWith(';') ? rawDdl : rawDdl + ";";
        }

        // Construct from parts
        var sb = new StringBuilder();
        sb.Append("CREATE ");
        if (index.IsUnique) sb.Append("UNIQUE ");
        sb.Append($"INDEX {Quote(index.Name)} ON {Quote(table.Schema)}.{Quote(table.TableName)}");

        if (!string.Equals(index.AccessMethod, "btree", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append($" USING {index.AccessMethod}");
        }

        var keyCols = string.Join(", ", index.Columns.Select(Quote));
        sb.Append($" ({keyCols})");

        if (index.IncludedColumns.Count > 0)
        {
            var inclCols = string.Join(", ", index.IncludedColumns.Select(Quote));
            sb.Append($" INCLUDE ({inclCols})");
        }

        if (!string.IsNullOrWhiteSpace(index.FilterExpression))
        {
            sb.Append($" WHERE {index.FilterExpression}");
        }

        sb.Append(';');
        return sb.ToString();
    }

    internal string GenerateCreateView(PgViewDefinition view)
    {
        var definition = view.Definition.TrimEnd().TrimEnd(';');

        if (view.IsMaterialized)
        {
            return $"CREATE MATERIALIZED VIEW {Quote(view.Schema)}.{Quote(view.Name)} AS {definition};";
        }

        return $"CREATE OR REPLACE VIEW {Quote(view.Schema)}.{Quote(view.Name)} AS {definition};";
    }

    internal string GenerateCreateFunction(PgFunctionDefinition function)
    {
        // pg_get_functiondef already includes the full CREATE OR REPLACE FUNCTION statement
        var definition = function.Definition.TrimEnd();
        return definition.EndsWith(';') ? definition : definition + ";";
    }

    private static string Quote(string name) => PgIdentifierHelper.QuoteIdentifier(name);

    private static readonly HashSet<string> ValidReferentialActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "NO ACTION", "CASCADE", "SET NULL", "SET DEFAULT", "RESTRICT"
    };

    private static string ValidateReferentialAction(string action)
    {
        if (ValidReferentialActions.Contains(action))
            return action.ToUpperInvariant();
        return "NO ACTION";
    }
}
