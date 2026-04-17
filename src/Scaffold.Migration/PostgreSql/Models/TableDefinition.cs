namespace Scaffold.Migration.PostgreSql.Models;

/// <summary>
/// Represents a SQL Server table definition with full column, constraint, and index details
/// needed for DDL translation to PostgreSQL.
/// </summary>
public class TableDefinition
{
    public string Schema { get; set; } = "dbo";
    public string TableName { get; set; } = string.Empty;
    public List<ColumnDefinition> Columns { get; set; } = [];
    public PrimaryKeyDefinition? PrimaryKey { get; set; }
    public List<IndexDefinition> Indexes { get; set; } = [];
    public List<ForeignKeyDefinition> ForeignKeys { get; set; } = [];
    public List<CheckConstraintDefinition> CheckConstraints { get; set; } = [];
    public List<UniqueConstraintDefinition> UniqueConstraints { get; set; } = [];
}

/// <summary>
/// Represents a column in a SQL Server table with full type and constraint metadata.
/// </summary>
public class ColumnDefinition
{
    public string Name { get; set; } = string.Empty;
    /// <summary>SQL Server data type name (e.g. "int", "nvarchar").</summary>
    public string DataType { get; set; } = string.Empty;
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public bool IsNullable { get; set; }
    public bool IsIdentity { get; set; }
    /// <summary>SQL Server default expression (e.g. "((0))", "(getdate())").</summary>
    public string? DefaultExpression { get; set; }
    public bool IsComputed { get; set; }
    public string? ComputedExpression { get; set; }
    public int OrdinalPosition { get; set; }
}

/// <summary>
/// Represents a primary key constraint on a table.
/// </summary>
public class PrimaryKeyDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = [];
    public bool IsClustered { get; set; }
}

/// <summary>
/// Represents a foreign key constraint between two tables.
/// </summary>
public class ForeignKeyDefinition
{
    public string Name { get; set; } = string.Empty;
    public string ReferencedSchema { get; set; } = "dbo";
    public string ReferencedTable { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = [];
    public List<string> ReferencedColumns { get; set; } = [];
    public string DeleteAction { get; set; } = "NO ACTION";
    public string UpdateAction { get; set; } = "NO ACTION";
}

/// <summary>
/// Represents a non-primary-key index on a table.
/// </summary>
public class IndexDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = [];
    public List<string> IncludedColumns { get; set; } = [];
    public bool IsUnique { get; set; }
    public bool IsClustered { get; set; }
    public string? FilterExpression { get; set; }
}

/// <summary>
/// Represents a CHECK constraint on a table.
/// </summary>
public class CheckConstraintDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
}

/// <summary>
/// Represents a UNIQUE constraint on a table.
/// </summary>
public class UniqueConstraintDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = [];
}