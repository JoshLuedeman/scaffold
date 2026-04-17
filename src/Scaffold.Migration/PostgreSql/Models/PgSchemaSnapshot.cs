namespace Scaffold.Migration.PostgreSql.Models;

/// <summary>
/// Snapshot of a PostgreSQL database schema, extracted from pg_catalog and information_schema.
/// Contains all objects needed to recreate the schema on a target database.
/// </summary>
public class PgSchemaSnapshot
{
    public List<PgTableDefinition> Tables { get; set; } = [];
    public List<PgViewDefinition> Views { get; set; } = [];
    public List<PgFunctionDefinition> Functions { get; set; } = [];
    public List<PgSequenceDefinition> Sequences { get; set; } = [];
    public List<PgEnumTypeDefinition> EnumTypes { get; set; } = [];
    public List<string> Extensions { get; set; } = [];
}
