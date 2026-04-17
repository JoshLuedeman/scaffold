namespace Scaffold.Migration.PostgreSql;

/// <summary>
/// Shared PostgreSQL identifier quoting and schema mapping utilities.
/// Consolidates duplicate logic from DdlTranslator, CrossPlatformBulkCopier,
/// and PostgreSqlValidationEngine.
/// </summary>
public static class PgIdentifierHelper
{
    /// <summary>
    /// Wraps an identifier in PostgreSQL double-quotes, escaping embedded quotes.
    /// </summary>
    public static string QuoteIdentifier(string name) => $"\"{name.Replace("\"", "\"\"")}\"";

    /// <summary>
    /// Maps SQL Server schema names to PostgreSQL equivalents.
    /// "dbo" → "public"; all others preserved.
    /// </summary>
    public static string MapSchema(string schema)
        => schema.Equals("dbo", StringComparison.OrdinalIgnoreCase) ? "public" : schema;

    /// <summary>
    /// Quotes a dotted table name for PostgreSQL: dbo.Users → "public"."Users".
    /// Maps "dbo" schema to "public". Escapes embedded double-quotes.
    /// Strips SQL Server bracket notation [name] and existing PG quotes.
    /// </summary>
    public static string QuotePgName(string tableName)
    {
        var parts = tableName.Split('.');
        if (parts.Length == 2 &&
            parts[0].Trim('[', ']', '"').Equals("dbo", StringComparison.OrdinalIgnoreCase))
        {
            parts[0] = "public";
        }

        return string.Join(".", parts.Select(p =>
        {
            var clean = p.Trim('[', ']', '"');
            return $"\"{clean.Replace("\"", "\"\"")}\"";
        }));
    }

    /// <summary>
    /// Quotes a dotted table name for SQL Server: dbo.Users → [dbo].[Users].
    /// Escapes embedded ']' characters by doubling them.
    /// </summary>
    public static string QuoteSqlName(string tableName)
    {
        var parts = tableName.Split('.');
        return string.Join(".", parts.Select(p =>
        {
            var clean = p.Trim('[', ']');
            return $"[{clean.Replace("]", "]]")}]";
        }));
    }

    /// <summary>
    /// Clamps a timeout value to [30, 3600], using defaultValue when value is null.
    /// </summary>
    public static int ClampTimeout(int? value, int defaultValue)
        => Math.Clamp(value ?? defaultValue, 30, 3600);
}
