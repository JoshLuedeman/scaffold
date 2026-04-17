using System.Text;
using Scaffold.Assessment.SqlServer;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.PostgreSql;

/// <summary>
/// Generates PostgreSQL-syntax canned migration scripts (pre and post migration).
/// Equivalent to <see cref="MigrationScriptGenerator"/> for SQL Server but targeting PostgreSQL.
/// </summary>
public static class PostgreSqlScriptGenerator
{
    // ── Pre-Migration Scripts ──────────────────────────────────────

    /// <summary>
    /// Generates ALTER TABLE ... DROP CONSTRAINT statements for all foreign keys using PG double-quoted syntax.
    /// </summary>
    public static string GenerateDropForeignKeys(SchemaInventory schema)
    {
        var fks = schema.Objects
            .Where(o => o.ObjectType == "Constraint" && o.SubType == "FOREIGN KEY")
            .ToList();

        if (fks.Count == 0) return "-- No foreign keys found";

        var sb = new StringBuilder();
        sb.AppendLine("-- Drop Foreign Keys (PostgreSQL)");
        sb.AppendLine($"-- {fks.Count} foreign key(s)");
        sb.AppendLine();
        foreach (var fk in fks)
        {
            var pgSchema = MapSchema(fk.Schema);
            sb.AppendLine($"ALTER TABLE \"{pgSchema}\".\"{fk.ParentObjectName}\" DROP CONSTRAINT IF EXISTS \"{fk.Name}\";");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Generates DROP INDEX IF EXISTS statements for all indexes using PG schema-scoped syntax.
    /// </summary>
    public static string GenerateDropIndexes(SchemaInventory schema)
    {
        var indexes = schema.Objects
            .Where(o => o.ObjectType == "Index" &&
                   o.SubType != null &&
                   o.SubType.Contains("NONCLUSTERED", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (indexes.Count == 0) return "-- No indexes found";

        var sb = new StringBuilder();
        sb.AppendLine("-- Drop Indexes (PostgreSQL)");
        sb.AppendLine($"-- {indexes.Count} index(es)");
        sb.AppendLine();
        foreach (var idx in indexes)
        {
            var pgSchema = MapSchema(idx.Schema);
            sb.AppendLine($"DROP INDEX IF EXISTS \"{pgSchema}\".\"{idx.Name}\";");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Generates ALTER TABLE ... DISABLE TRIGGER ALL for each table with triggers.
    /// </summary>
    public static string GenerateDisableTriggers(SchemaInventory schema)
    {
        var triggers = schema.Objects
            .Where(o => o.ObjectType == "Trigger")
            .ToList();

        if (triggers.Count == 0) return "-- No triggers found";

        // Group by parent table to avoid duplicate DISABLE TRIGGER ALL per table
        var tables = triggers
            .Select(t => new { Schema = MapSchema(t.Schema), Table = t.ParentObjectName })
            .Distinct()
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("-- Disable Triggers (PostgreSQL)");
        sb.AppendLine($"-- {triggers.Count} trigger(s) on {tables.Count} table(s)");
        sb.AppendLine();
        foreach (var tbl in tables)
        {
            sb.AppendLine($"ALTER TABLE \"{tbl.Schema}\".\"{tbl.Table}\" DISABLE TRIGGER ALL;");
        }
        return sb.ToString().TrimEnd();
    }

    // ── Post-Migration Scripts ─────────────────────────────────────

    /// <summary>
    /// Generates ALTER TABLE ... ADD CONSTRAINT ... FOREIGN KEY statements with PG syntax.
    /// </summary>
    public static string GenerateCreateForeignKeys(SchemaInventory schema)
    {
        var fks = schema.Objects
            .Where(o => o.ObjectType == "Constraint" && o.SubType == "FOREIGN KEY")
            .ToList();

        if (fks.Count == 0) return "-- No foreign keys to create";

        var sb = new StringBuilder();
        sb.AppendLine("-- Create Foreign Keys (PostgreSQL)");
        sb.AppendLine($"-- {fks.Count} foreign key(s)");
        sb.AppendLine();
        foreach (var fk in fks)
        {
            if (!string.IsNullOrEmpty(fk.Columns) && !string.IsNullOrEmpty(fk.ReferencedTable) && !string.IsNullOrEmpty(fk.ReferencedColumns))
            {
                var pgSchema = MapSchema(fk.Schema);
                var refSchema = MapSchema(fk.ReferencedSchema ?? "dbo");
                var fkCols = FormatColumnList(fk.Columns);
                var refCols = FormatColumnList(fk.ReferencedColumns);

                sb.AppendLine($"ALTER TABLE \"{pgSchema}\".\"{fk.ParentObjectName}\" ADD CONSTRAINT \"{fk.Name}\"");
                sb.AppendLine($"    FOREIGN KEY ({fkCols}) REFERENCES \"{refSchema}\".\"{fk.ReferencedTable}\" ({refCols})");
                sb.Append($"    ON DELETE {MapReferentialAction(fk.DeleteAction)} ON UPDATE {MapReferentialAction(fk.UpdateAction)}");
                sb.AppendLine(";");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine($"-- WARNING: Missing FK metadata for \"{fk.Name}\" on \"{fk.ParentObjectName}\". Manual creation required.");
            }
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Generates CREATE INDEX stub statements. Since SchemaObject has limited column info,
    /// these are generated as comments with guidance for manual creation.
    /// </summary>
    public static string GenerateCreateIndexes(SchemaInventory schema)
    {
        var indexes = schema.Objects
            .Where(o => o.ObjectType == "Index" &&
                   o.SubType != null &&
                   o.SubType.Contains("NONCLUSTERED", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (indexes.Count == 0) return "-- No indexes to create";

        var sb = new StringBuilder();
        sb.AppendLine("-- Create Indexes (PostgreSQL)");
        sb.AppendLine($"-- {indexes.Count} index(es)");
        sb.AppendLine("-- NOTE: Column details not available from schema inventory.");
        sb.AppendLine("-- Use DDL translator for full index creation from source schema.");
        sb.AppendLine();
        foreach (var idx in indexes)
        {
            var pgSchema = MapSchema(idx.Schema);
            sb.AppendLine($"-- CREATE INDEX \"{idx.Name}\" ON \"{pgSchema}\".\"{idx.ParentObjectName}\" (<columns>);");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Generates ALTER TABLE ... ENABLE TRIGGER ALL for each table with triggers.
    /// </summary>
    public static string GenerateEnableTriggers(SchemaInventory schema)
    {
        var triggers = schema.Objects
            .Where(o => o.ObjectType == "Trigger")
            .ToList();

        if (triggers.Count == 0) return "-- No triggers found";

        var tables = triggers
            .Select(t => new { Schema = MapSchema(t.Schema), Table = t.ParentObjectName })
            .Distinct()
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("-- Enable Triggers (PostgreSQL)");
        sb.AppendLine($"-- {triggers.Count} trigger(s) on {tables.Count} table(s)");
        sb.AppendLine();
        foreach (var tbl in tables)
        {
            sb.AppendLine($"ALTER TABLE \"{tbl.Schema}\".\"{tbl.Table}\" ENABLE TRIGGER ALL;");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Generates ANALYZE statements for all tables (PG equivalent of UPDATE STATISTICS).
    /// </summary>
    public static string GenerateAnalyzeTables(SchemaInventory schema)
    {
        var tables = schema.Objects
            .Where(o => o.ObjectType == "Table")
            .ToList();

        if (tables.Count == 0) return "-- No tables found";

        var sb = new StringBuilder();
        sb.AppendLine("-- Analyze Tables (PostgreSQL)");
        sb.AppendLine($"-- {tables.Count} table(s)");
        sb.AppendLine();
        foreach (var tbl in tables)
        {
            var pgSchema = MapSchema(tbl.Schema);
            sb.AppendLine($"ANALYZE \"{pgSchema}\".\"{tbl.Name}\";");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Generates SELECT COUNT(*) validation queries for each table using PG syntax.
    /// </summary>
    public static string GenerateValidateRowCounts(SchemaInventory schema)
    {
        var tables = schema.Objects
            .Where(o => o.ObjectType == "Table")
            .ToList();

        if (tables.Count == 0) return "-- No tables found";

        var sb = new StringBuilder();
        sb.AppendLine("-- Validate Row Counts (PostgreSQL)");
        sb.AppendLine($"-- {tables.Count} table(s)");
        sb.AppendLine("-- Run this on the target database and compare with source counts");
        sb.AppendLine();
        foreach (var tbl in tables)
        {
            var pgSchema = MapSchema(tbl.Schema);
            sb.AppendLine($"SELECT '{pgSchema}.{tbl.Name}' AS table_name, COUNT(*) AS row_count FROM \"{pgSchema}\".\"{tbl.Name}\";");
        }
        return sb.ToString().TrimEnd();
    }

    // ── Lookup ─────────────────────────────────────────────────────

    /// <summary>Returns the generated SQL for a canned script ID, or null if unknown.</summary>
    public static string? Generate(string scriptId, SchemaInventory schema)
    {
        return scriptId switch
        {
            "pg-drop-foreign-keys" => GenerateDropForeignKeys(schema),
            "pg-drop-indexes" => GenerateDropIndexes(schema),
            "pg-disable-triggers" => GenerateDisableTriggers(schema),
            "pg-create-foreign-keys" => GenerateCreateForeignKeys(schema),
            "pg-create-indexes" => GenerateCreateIndexes(schema),
            "pg-enable-triggers" => GenerateEnableTriggers(schema),
            "pg-analyze-tables" => GenerateAnalyzeTables(schema),
            "pg-validate-row-counts" => GenerateValidateRowCounts(schema),
            _ => null
        };
    }

    /// <summary>Returns all available PG canned script definitions with object counts.</summary>
    public static List<CannedScriptInfo> GetAvailableScripts(SchemaInventory schema)
    {
        var fkCount = schema.Objects.Count(o => o.ObjectType == "Constraint" && o.SubType == "FOREIGN KEY");
        var ncIndexCount = schema.Objects.Count(o => o.ObjectType == "Index" && o.SubType != null && o.SubType.Contains("NONCLUSTERED", StringComparison.OrdinalIgnoreCase));
        var triggerCount = schema.Objects.Count(o => o.ObjectType == "Trigger");
        var tableCount = schema.Objects.Count(o => o.ObjectType == "Table");

        return
        [
            new("pg-drop-foreign-keys", "Drop Foreign Keys (PG)", "Pre", $"Drops {fkCount} FK constraint(s) using PostgreSQL syntax", fkCount),
            new("pg-drop-indexes", "Drop Indexes (PG)", "Pre", $"Drops {ncIndexCount} index(es) using PostgreSQL syntax", ncIndexCount),
            new("pg-disable-triggers", "Disable Triggers (PG)", "Pre", $"Disables triggers on tables with {triggerCount} trigger(s)", triggerCount),

            new("pg-create-foreign-keys", "Create Foreign Keys (PG)", "Post", $"Creates {fkCount} FK constraint(s) using PostgreSQL syntax", fkCount),
            new("pg-create-indexes", "Create Indexes (PG)", "Post", $"Creates {ncIndexCount} index stub(s) (requires column details)", ncIndexCount),
            new("pg-enable-triggers", "Enable Triggers (PG)", "Post", $"Enables triggers on tables with {triggerCount} trigger(s)", triggerCount),
            new("pg-analyze-tables", "Analyze Tables (PG)", "Post", $"Runs ANALYZE on {tableCount} table(s) to update statistics", tableCount),
            new("pg-validate-row-counts", "Validate Row Counts (PG)", "Post", $"Generates row count queries for {tableCount} table(s)", tableCount),
        ];
    }

    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>Formats comma-separated column names with PostgreSQL double-quotes.</summary>
    private static string FormatColumnList(string columns)
        => string.Join(", ", columns.Split(',').Select(c => $"\"{c.Trim()}\""));

    /// <summary>Maps SQL Server schema to PostgreSQL (dbo -> public, others preserved).</summary>
    internal static string MapSchema(string schema)
        => string.Equals(schema, "dbo", StringComparison.OrdinalIgnoreCase) ? "public" : schema;

    /// <summary>Maps SQL Server referential action descriptions to SQL standard keywords.</summary>
    private static string MapReferentialAction(string? action)
    {
        if (string.IsNullOrEmpty(action)) return "NO ACTION";
        return action.Replace("_", " ").ToUpperInvariant() switch
        {
            "NO ACTION" => "NO ACTION",
            "CASCADE" => "CASCADE",
            "SET NULL" => "SET NULL",
            "SET DEFAULT" => "SET DEFAULT",
            _ => "NO ACTION"
        };
    }
}