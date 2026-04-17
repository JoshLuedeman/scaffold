namespace Scaffold.Migration.PostgreSql.Models;

/// <summary>
/// PostgreSQL-native table definition with PG-specific schema metadata.
/// Used by the PG→PG migration path (schema extractor, DDL generator, bulk copier).
/// Unlike the SQL Server-oriented <see cref="TableDefinition"/>, this uses PG-native
/// naming and concepts (schemas default to "public", identity uses GENERATED, etc.).
/// </summary>
public class PgTableDefinition
{
    public string Schema { get; set; } = "public";
    public string TableName { get; set; } = string.Empty;
    public List<PgColumnDefinition> Columns { get; set; } = [];
    public PgPrimaryKeyDefinition? PrimaryKey { get; set; }
    public List<PgIndexDefinition> Indexes { get; set; } = [];
    public List<PgForeignKeyDefinition> ForeignKeys { get; set; } = [];
    public List<PgCheckConstraintDefinition> CheckConstraints { get; set; } = [];
    public List<PgUniqueConstraintDefinition> UniqueConstraints { get; set; } = [];
    public List<PgSequenceDefinition> OwnedSequences { get; set; } = [];

    /// <summary>Fully qualified name: schema.table</summary>
    public string QualifiedName => $"{Schema}.{TableName}";
}

public class PgColumnDefinition
{
    public string Name { get; set; } = string.Empty;
    /// <summary>PostgreSQL data type (e.g., "integer", "text", "timestamp with time zone").</summary>
    public string DataType { get; set; } = string.Empty;
    /// <summary>Full type with modifiers (e.g., "character varying(255)").</summary>
    public string FullType { get; set; } = string.Empty;
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public bool IsNullable { get; set; }
    public bool IsIdentity { get; set; }
    /// <summary>"ALWAYS" or "BY DEFAULT"</summary>
    public string? IdentityGeneration { get; set; }
    /// <summary>PG default expression (e.g., "now()", "'active'::text").</summary>
    public string? DefaultExpression { get; set; }
    public bool IsGenerated { get; set; }
    /// <summary>Expression for GENERATED ALWAYS AS (stored) columns.</summary>
    public string? GenerationExpression { get; set; }
    public int OrdinalPosition { get; set; }
    public string? Collation { get; set; }
    /// <summary>For enum/composite/domain types, the UDT name.</summary>
    public string? UdtName { get; set; }
}

public class PgPrimaryKeyDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = [];
}

public class PgForeignKeyDefinition
{
    public string Name { get; set; } = string.Empty;
    public string ReferencedSchema { get; set; } = "public";
    public string ReferencedTable { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = [];
    public List<string> ReferencedColumns { get; set; } = [];
    public string DeleteAction { get; set; } = "NO ACTION";
    public string UpdateAction { get; set; } = "NO ACTION";
}

public class PgIndexDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = [];
    public List<string> IncludedColumns { get; set; } = [];
    public bool IsUnique { get; set; }
    /// <summary>PG index method: btree, hash, gin, gist, etc.</summary>
    public string AccessMethod { get; set; } = "btree";
    public string? FilterExpression { get; set; }
    /// <summary>Raw CREATE INDEX DDL for complex indexes that can't be decomposed.</summary>
    public string? RawDdl { get; set; }
}

public class PgCheckConstraintDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
}

public class PgUniqueConstraintDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = [];
}

public class PgSequenceDefinition
{
    public string Schema { get; set; } = "public";
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = "bigint";
    public long StartValue { get; set; } = 1;
    public long IncrementBy { get; set; } = 1;
    public long? MinValue { get; set; }
    public long? MaxValue { get; set; }
    public bool IsCyclic { get; set; }
    /// <summary>Table.Column this sequence is owned by (e.g., "users.id").</summary>
    public string? OwnedBy { get; set; }
}

/// <summary>
/// Represents a PostgreSQL view definition.
/// </summary>
public class PgViewDefinition
{
    public string Schema { get; set; } = "public";
    public string Name { get; set; } = string.Empty;
    /// <summary>Raw view DDL (CREATE OR REPLACE VIEW ...).</summary>
    public string Definition { get; set; } = string.Empty;
    public bool IsMaterialized { get; set; }
}

/// <summary>
/// Represents a PostgreSQL function/procedure definition.
/// </summary>
public class PgFunctionDefinition
{
    public string Schema { get; set; } = "public";
    public string Name { get; set; } = string.Empty;
    /// <summary>Raw function DDL (CREATE OR REPLACE FUNCTION ...).</summary>
    public string Definition { get; set; } = string.Empty;
    public string Language { get; set; } = "plpgsql";
    public string Kind { get; set; } = "f"; // f=function, p=procedure
}

/// <summary>
/// Represents a PostgreSQL custom enum type.
/// </summary>
public class PgEnumTypeDefinition
{
    public string Schema { get; set; } = "public";
    public string Name { get; set; } = string.Empty;
    public List<string> Labels { get; set; } = [];
}
