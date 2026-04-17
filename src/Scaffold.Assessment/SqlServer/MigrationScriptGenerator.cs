using System.Text;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.SqlServer;

public static class MigrationScriptGenerator
{
    // ── Pre-Migration Scripts ──────────────────────────────────────

    public static string GenerateDropForeignKeys(SchemaInventory schema)
    {
        var fks = schema.Objects
            .Where(o => o.ObjectType == "Constraint" && o.SubType == "FOREIGN KEY")
            .ToList();

        if (fks.Count == 0) return "-- No foreign keys found";

        var sb = new StringBuilder();
        sb.AppendLine("-- Drop Foreign Keys");
        sb.AppendLine($"-- {fks.Count} foreign key(s)");
        sb.AppendLine();
        foreach (var fk in fks)
        {
            sb.AppendLine($"ALTER TABLE [{fk.Schema}].[{fk.ParentObjectName}] DROP CONSTRAINT [{fk.Name}];");
        }
        return sb.ToString().TrimEnd();
    }

    public static string GenerateDropNonClusteredIndexes(SchemaInventory schema)
    {
        var indexes = schema.Objects
            .Where(o => o.ObjectType == "Index" && 
                   o.SubType != null && 
                   o.SubType.Contains("NONCLUSTERED", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (indexes.Count == 0) return "-- No non-clustered indexes found";

        var sb = new StringBuilder();
        sb.AppendLine("-- Drop Non-Clustered Indexes");
        sb.AppendLine($"-- {indexes.Count} index(es)");
        sb.AppendLine();
        foreach (var idx in indexes)
        {
            sb.AppendLine($"DROP INDEX [{idx.Name}] ON [{idx.Schema}].[{idx.ParentObjectName}];");
        }
        return sb.ToString().TrimEnd();
    }

    public static string GenerateDropTriggers(SchemaInventory schema)
    {
        var triggers = schema.Objects
            .Where(o => o.ObjectType == "Trigger")
            .ToList();

        if (triggers.Count == 0) return "-- No triggers found";

        var sb = new StringBuilder();
        sb.AppendLine("-- Drop Triggers");
        sb.AppendLine($"-- {triggers.Count} trigger(s)");
        sb.AppendLine();
        foreach (var trg in triggers)
        {
            sb.AppendLine($"DROP TRIGGER [{trg.Schema}].[{trg.Name}];");
        }
        return sb.ToString().TrimEnd();
    }

    public static string GenerateDisableCheckConstraints(SchemaInventory schema)
    {
        var checks = schema.Objects
            .Where(o => o.ObjectType == "Constraint" && o.SubType == "CHECK")
            .ToList();

        if (checks.Count == 0) return "-- No check constraints found";

        var sb = new StringBuilder();
        sb.AppendLine("-- Disable Check Constraints");
        sb.AppendLine($"-- {checks.Count} check constraint(s)");
        sb.AppendLine();
        foreach (var chk in checks)
        {
            sb.AppendLine($"ALTER TABLE [{chk.Schema}].[{chk.ParentObjectName}] NOCHECK CONSTRAINT [{chk.Name}];");
        }
        return sb.ToString().TrimEnd();
    }

    // ── Post-Migration Scripts ─────────────────────────────────────

    public static string GenerateApplyForeignKeys(SchemaInventory schema)
    {
        var fks = schema.Objects
            .Where(o => o.ObjectType == "Constraint" && o.SubType == "FOREIGN KEY")
            .ToList();

        if (fks.Count == 0) return "-- No foreign keys to restore";

        var sb = new StringBuilder();
        sb.AppendLine("-- Re-create Foreign Keys");
        sb.AppendLine($"-- {fks.Count} foreign key(s)");
        sb.AppendLine();
        foreach (var fk in fks)
        {
            if (!string.IsNullOrEmpty(fk.Columns) && !string.IsNullOrEmpty(fk.ReferencedTable) && !string.IsNullOrEmpty(fk.ReferencedColumns))
            {
                var refSchema = fk.ReferencedSchema ?? "dbo";
                var fkColumns = FormatColumnList(fk.Columns);
                var refColumns = FormatColumnList(fk.ReferencedColumns);

                sb.AppendLine($"ALTER TABLE [{fk.Schema}].[{fk.ParentObjectName}] ADD CONSTRAINT [{fk.Name}]");
                sb.AppendLine($"    FOREIGN KEY ({fkColumns}) REFERENCES [{refSchema}].[{fk.ReferencedTable}] ({refColumns})");
                sb.Append($"    ON DELETE {MapReferentialAction(fk.DeleteAction)} ON UPDATE {MapReferentialAction(fk.UpdateAction)}");
                sb.AppendLine(";");
                sb.AppendLine();
            }
            else
            {
                // Fallback: re-enable existing constraint when FK metadata is missing
                sb.AppendLine($"ALTER TABLE [{fk.Schema}].[{fk.ParentObjectName}] WITH CHECK CHECK CONSTRAINT [{fk.Name}];");
            }
        }
        return sb.ToString().TrimEnd();
    }

    public static string GenerateCreateForeignKeysPostgreSql(SchemaInventory schema)
    {
        var fks = schema.Objects
            .Where(o => o.ObjectType == "Constraint" && o.SubType == "FOREIGN KEY")
            .ToList();

        if (fks.Count == 0) return "-- No foreign keys to create";

        var sb = new StringBuilder();
        sb.AppendLine("-- Create Foreign Keys (PostgreSQL syntax)");
        sb.AppendLine($"-- {fks.Count} foreign key(s)");
        sb.AppendLine();
        foreach (var fk in fks)
        {
            if (!string.IsNullOrEmpty(fk.Columns) && !string.IsNullOrEmpty(fk.ReferencedTable) && !string.IsNullOrEmpty(fk.ReferencedColumns))
            {
                var refSchema = fk.ReferencedSchema ?? "public";
                var fkColumns = FormatColumnListPg(fk.Columns);
                var refColumns = FormatColumnListPg(fk.ReferencedColumns);
                var parentSchema = MapSchemaToPg(fk.Schema);

                sb.AppendLine($"ALTER TABLE \"{parentSchema}\".\"{fk.ParentObjectName}\" ADD CONSTRAINT \"{fk.Name}\"");
                sb.AppendLine($"    FOREIGN KEY ({fkColumns}) REFERENCES \"{refSchema}\".\"{fk.ReferencedTable}\" ({refColumns})");
                sb.Append($"    ON DELETE {MapReferentialAction(fk.DeleteAction)} ON UPDATE {MapReferentialAction(fk.UpdateAction)}");
                sb.AppendLine(";");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine($"-- WARNING: Missing FK metadata for [{fk.Name}] on [{fk.ParentObjectName}]. Manual creation required.");
            }
        }
        return sb.ToString().TrimEnd();
    }

    public static string GenerateApplyNonClusteredIndexes(SchemaInventory schema)
    {
        // Like FKs, we can only generate stubs — full CREATE INDEX needs column info
        var indexes = schema.Objects
            .Where(o => o.ObjectType == "Index" && 
                   o.SubType != null && 
                   o.SubType.Contains("NONCLUSTERED", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (indexes.Count == 0) return "-- No non-clustered indexes to rebuild";

        var sb = new StringBuilder();
        sb.AppendLine("-- Rebuild Non-Clustered Indexes");
        sb.AppendLine($"-- {indexes.Count} index(es)");
        sb.AppendLine("-- NOTE: If indexes were dropped, re-run schema migration to recreate them.");
        sb.AppendLine("-- If indexes exist but need optimization, use ALTER INDEX REBUILD:");
        sb.AppendLine();
        foreach (var idx in indexes)
        {
            sb.AppendLine($"ALTER INDEX [{idx.Name}] ON [{idx.Schema}].[{idx.ParentObjectName}] REBUILD;");
        }
        return sb.ToString().TrimEnd();
    }

    public static string GenerateApplyTriggers(SchemaInventory schema)
    {
        var triggers = schema.Objects
            .Where(o => o.ObjectType == "Trigger")
            .ToList();

        if (triggers.Count == 0) return "-- No triggers to re-enable";

        var sb = new StringBuilder();
        sb.AppendLine("-- Re-enable Triggers");
        sb.AppendLine($"-- {triggers.Count} trigger(s)");
        sb.AppendLine();
        foreach (var trg in triggers)
        {
            sb.AppendLine($"ENABLE TRIGGER [{trg.Name}] ON [{trg.Schema}].[{trg.ParentObjectName}];");
        }
        return sb.ToString().TrimEnd();
    }

    public static string GenerateEnableCheckConstraints(SchemaInventory schema)
    {
        var checks = schema.Objects
            .Where(o => o.ObjectType == "Constraint" && o.SubType == "CHECK")
            .ToList();

        if (checks.Count == 0) return "-- No check constraints to re-enable";

        var sb = new StringBuilder();
        sb.AppendLine("-- Re-enable Check Constraints with validation");
        sb.AppendLine($"-- {checks.Count} check constraint(s)");
        sb.AppendLine();
        foreach (var chk in checks)
        {
            sb.AppendLine($"ALTER TABLE [{chk.Schema}].[{chk.ParentObjectName}] WITH CHECK CHECK CONSTRAINT [{chk.Name}];");
        }
        return sb.ToString().TrimEnd();
    }

    public static string GenerateUpdateStatistics(SchemaInventory schema)
    {
        var tables = schema.Objects
            .Where(o => o.ObjectType == "Table")
            .ToList();

        if (tables.Count == 0) return "-- No tables found";

        var sb = new StringBuilder();
        sb.AppendLine("-- Update Statistics");
        sb.AppendLine($"-- {tables.Count} table(s)");
        sb.AppendLine();
        foreach (var tbl in tables)
        {
            sb.AppendLine($"UPDATE STATISTICS [{tbl.Schema}].[{tbl.Name}];");
        }
        return sb.ToString().TrimEnd();
    }

    public static string GenerateValidateRowCounts(SchemaInventory schema)
    {
        var tables = schema.Objects
            .Where(o => o.ObjectType == "Table")
            .ToList();

        if (tables.Count == 0) return "-- No tables found";

        var sb = new StringBuilder();
        sb.AppendLine("-- Validate Row Counts");
        sb.AppendLine($"-- {tables.Count} table(s)");
        sb.AppendLine("-- Run this on the target database and compare with source counts");
        sb.AppendLine();
        foreach (var tbl in tables)
        {
            sb.AppendLine($"SELECT '{tbl.Schema}.{tbl.Name}' AS TableName, COUNT(*) AS RowCount FROM [{tbl.Schema}].[{tbl.Name}];");
        }
        return sb.ToString().TrimEnd();
    }

    // ── Lookup ─────────────────────────────────────────────────────

    /// <summary>Returns the generated SQL for a canned script ID, or null if unknown.</summary>
    public static string? Generate(string scriptId, SchemaInventory schema)
    {
        return scriptId switch
        {
            "drop-foreign-keys" => GenerateDropForeignKeys(schema),
            "drop-nonclustered-indexes" => GenerateDropNonClusteredIndexes(schema),
            "drop-triggers" => GenerateDropTriggers(schema),
            "disable-check-constraints" => GenerateDisableCheckConstraints(schema),
            "apply-foreign-keys" => GenerateApplyForeignKeys(schema),
            "create-foreign-keys" => GenerateApplyForeignKeys(schema),
            "create-foreign-keys-pg" => GenerateCreateForeignKeysPostgreSql(schema),
            "apply-nonclustered-indexes" => GenerateApplyNonClusteredIndexes(schema),
            "apply-triggers" => GenerateApplyTriggers(schema),
            "enable-check-constraints" => GenerateEnableCheckConstraints(schema),
            "update-statistics" => GenerateUpdateStatistics(schema),
            "validate-row-counts" => GenerateValidateRowCounts(schema),
            _ => null
        };
    }

    /// <summary>Returns all available canned script definitions with object counts.</summary>
    public static List<CannedScriptInfo> GetAvailableScripts(SchemaInventory schema)
    {
        var fkCount = schema.Objects.Count(o => o.ObjectType == "Constraint" && o.SubType == "FOREIGN KEY");
        var ncIndexCount = schema.Objects.Count(o => o.ObjectType == "Index" && o.SubType != null && o.SubType.Contains("NONCLUSTERED", StringComparison.OrdinalIgnoreCase));
        var triggerCount = schema.Objects.Count(o => o.ObjectType == "Trigger");
        var checkCount = schema.Objects.Count(o => o.ObjectType == "Constraint" && o.SubType == "CHECK");
        var tableCount = schema.Objects.Count(o => o.ObjectType == "Table");

        return
        [
            new("drop-foreign-keys", "Drop Foreign Keys", "Pre", $"Removes {fkCount} FK constraint(s) so data loads in any order", fkCount),
            new("drop-nonclustered-indexes", "Drop Non-Clustered Indexes", "Pre", $"Removes {ncIndexCount} non-clustered index(es) to speed up bulk insert", ncIndexCount),
            new("drop-triggers", "Drop Triggers", "Pre", $"Removes {triggerCount} trigger(s) to prevent firing during bulk load", triggerCount),
            new("disable-check-constraints", "Disable Check Constraints", "Pre", $"Disables {checkCount} check constraint(s) during data load", checkCount),

            new("apply-foreign-keys", "Apply Foreign Keys", "Post", $"Re-creates {fkCount} FK constraint(s) with full definitions", fkCount),
            new("create-foreign-keys", "Create Foreign Keys (SQL Server)", "Post", $"Creates {fkCount} FK constraint(s) using SQL Server syntax", fkCount),
            new("create-foreign-keys-pg", "Create Foreign Keys (PostgreSQL)", "Post", $"Creates {fkCount} FK constraint(s) using PostgreSQL syntax", fkCount),
            new("apply-nonclustered-indexes", "Apply Non-Clustered Indexes", "Post", $"Rebuilds {ncIndexCount} non-clustered index(es)", ncIndexCount),
            new("apply-triggers", "Apply Triggers", "Post", $"Re-enables {triggerCount} trigger(s)", triggerCount),
            new("enable-check-constraints", "Enable Check Constraints", "Post", $"Re-enables {checkCount} check constraint(s) with validation", checkCount),
            new("update-statistics", "Update Statistics", "Post", $"Updates statistics on {tableCount} table(s)", tableCount),
            new("validate-row-counts", "Validate Row Counts", "Post", $"Generates row count queries for {tableCount} table(s)", tableCount),
        ];
    }
    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>Formats comma-separated column names with SQL Server brackets.</summary>
    private static string FormatColumnList(string columns)
        => string.Join(", ", columns.Split(',').Select(c => $"[{c.Trim()}]"));

    /// <summary>Formats comma-separated column names with PostgreSQL double-quotes.</summary>
    private static string FormatColumnListPg(string columns)
        => string.Join(", ", columns.Split(',').Select(c => $"\"{c.Trim()}\""));

    /// <summary>Maps SQL Server schema to PostgreSQL (dbo → public, others kept as-is).</summary>
    private static string MapSchemaToPg(string schema)
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

public record CannedScriptInfo(string ScriptId, string Label, string Phase, string Description, int ObjectCount);
