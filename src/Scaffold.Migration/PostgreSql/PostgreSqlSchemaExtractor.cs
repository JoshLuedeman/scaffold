using Npgsql;
using Scaffold.Core.Interfaces;
using Scaffold.Migration.PostgreSql.Models;

namespace Scaffold.Migration.PostgreSql;

/// <summary>
/// Reads schema from a PostgreSQL source database and produces PG-native model objects.
/// Queries pg_catalog and information_schema to extract tables, views, functions,
/// sequences, enum types, indexes, and extensions.
/// </summary>
public class PostgreSqlSchemaExtractor
{
    /// <summary>
    /// Extracts full schema from a PostgreSQL database.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="includedObjects">Optional filter: only include tables matching these schema.table names.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A snapshot of the database schema.</returns>
    public virtual async Task<PgSchemaSnapshot> ExtractSchemaAsync(
        string connectionString,
        IReadOnlyList<string>? includedObjects = null,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var snapshot = new PgSchemaSnapshot();

        progress?.Report(new MigrationProgress
        {
            Phase = "SchemaExtraction",
            PercentComplete = 0,
            Message = "Extracting extensions..."
        });

        snapshot.Extensions = await ReadExtensionsAsync(connection, ct);

        progress?.Report(new MigrationProgress
        {
            Phase = "SchemaExtraction",
            PercentComplete = 10,
            Message = "Extracting enum types..."
        });

        snapshot.EnumTypes = await ReadEnumTypesAsync(connection, ct);

        progress?.Report(new MigrationProgress
        {
            Phase = "SchemaExtraction",
            PercentComplete = 20,
            Message = "Extracting tables and columns..."
        });

        snapshot.Tables = await ReadTablesAndColumnsAsync(connection, includedObjects, ct);
        var tableMap = snapshot.Tables.ToDictionary(
            t => t.QualifiedName, StringComparer.OrdinalIgnoreCase);

        progress?.Report(new MigrationProgress
        {
            Phase = "SchemaExtraction",
            PercentComplete = 40,
            Message = "Extracting primary keys..."
        });

        await ReadPrimaryKeysAsync(connection, tableMap, ct);

        progress?.Report(new MigrationProgress
        {
            Phase = "SchemaExtraction",
            PercentComplete = 50,
            Message = "Extracting foreign keys..."
        });

        await ReadForeignKeysAsync(connection, tableMap, ct);

        progress?.Report(new MigrationProgress
        {
            Phase = "SchemaExtraction",
            PercentComplete = 55,
            Message = "Extracting unique constraints..."
        });

        await ReadUniqueConstraintsAsync(connection, tableMap, ct);

        progress?.Report(new MigrationProgress
        {
            Phase = "SchemaExtraction",
            PercentComplete = 60,
            Message = "Extracting check constraints..."
        });

        await ReadCheckConstraintsAsync(connection, tableMap, ct);

        progress?.Report(new MigrationProgress
        {
            Phase = "SchemaExtraction",
            PercentComplete = 65,
            Message = "Extracting indexes..."
        });

        await ReadIndexesAsync(connection, tableMap, ct);

        progress?.Report(new MigrationProgress
        {
            Phase = "SchemaExtraction",
            PercentComplete = 75,
            Message = "Extracting sequences..."
        });

        snapshot.Sequences = await ReadSequencesAsync(connection, ct);

        progress?.Report(new MigrationProgress
        {
            Phase = "SchemaExtraction",
            PercentComplete = 85,
            Message = "Extracting views..."
        });

        snapshot.Views = await ReadViewsAsync(connection, ct);

        progress?.Report(new MigrationProgress
        {
            Phase = "SchemaExtraction",
            PercentComplete = 95,
            Message = "Extracting functions..."
        });

        snapshot.Functions = await ReadFunctionsAsync(connection, ct);

        progress?.Report(new MigrationProgress
        {
            Phase = "SchemaExtraction",
            PercentComplete = 100,
            Message = "Schema extraction complete."
        });

        return snapshot;
    }

    internal static async Task<List<string>> ReadExtensionsAsync(
        NpgsqlConnection connection, CancellationToken ct)
    {
        const string sql = "SELECT extname FROM pg_extension WHERE extname != 'plpgsql'";

        var extensions = new List<string>();
        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            extensions.Add(reader.GetString(0));
        }

        return extensions;
    }

    internal static async Task<List<PgEnumTypeDefinition>> ReadEnumTypesAsync(
        NpgsqlConnection connection, CancellationToken ct)
    {
        const string sql = """
            SELECT n.nspname AS schema, t.typname AS name,
                   e.enumlabel AS label, e.enumsortorder
            FROM pg_type t
            JOIN pg_enum e ON t.oid = e.enumtypid
            JOIN pg_namespace n ON t.typnamespace = n.oid
            WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
            ORDER BY n.nspname, t.typname, e.enumsortorder
            """;

        var enums = new List<PgEnumTypeDefinition>();
        var enumMap = new Dictionary<string, PgEnumTypeDefinition>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var schema = reader.GetString(0);
            var name = reader.GetString(1);
            var label = reader.GetString(2);
            var key = $"{schema}.{name}";

            if (!enumMap.TryGetValue(key, out var enumDef))
            {
                enumDef = new PgEnumTypeDefinition { Schema = schema, Name = name };
                enumMap[key] = enumDef;
                enums.Add(enumDef);
            }

            enumDef.Labels.Add(label);
        }

        return enums;
    }

    internal static async Task<List<PgTableDefinition>> ReadTablesAndColumnsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<string>? includedObjects,
        CancellationToken ct)
    {
        const string sql = """
            SELECT c.table_schema, c.table_name, c.column_name, c.data_type, c.udt_name,
                   c.character_maximum_length, c.numeric_precision, c.numeric_scale,
                   c.is_nullable, c.column_default, c.ordinal_position,
                   c.is_identity, c.identity_generation, c.is_generated, c.generation_expression,
                   c.collation_name, c.udt_schema
            FROM information_schema.columns c
            JOIN information_schema.tables t
              ON c.table_schema = t.table_schema AND c.table_name = t.table_name
            WHERE t.table_type = 'BASE TABLE'
              AND c.table_schema NOT IN ('pg_catalog', 'information_schema', 'pg_toast')
            ORDER BY c.table_schema, c.table_name, c.ordinal_position
            """;

        var tables = new List<PgTableDefinition>();
        var tableMap = new Dictionary<string, PgTableDefinition>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var schema = reader.GetString(0);
            var tableName = reader.GetString(1);
            var qualifiedName = $"{schema}.{tableName}";

            if (includedObjects is { Count: > 0 } &&
                !IsObjectIncluded(schema, tableName, includedObjects))
                continue;

            if (!tableMap.TryGetValue(qualifiedName, out var table))
            {
                table = new PgTableDefinition { Schema = schema, TableName = tableName };
                tableMap[qualifiedName] = table;
                tables.Add(table);
            }

            var column = BuildColumnDefinition(reader);
            table.Columns.Add(column);
        }

        return tables;
    }

    internal static PgColumnDefinition BuildColumnDefinition(NpgsqlDataReader reader)
    {
        var dataType = reader.GetString(3);
        var udtName = reader.IsDBNull(4) ? null : reader.GetString(4);
        var maxLength = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
        var precision = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6);
        var scale = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7);
        var udtSchema = reader.IsDBNull(16) ? null : reader.GetString(16);

        return new PgColumnDefinition
        {
            Name = reader.GetString(2),
            DataType = dataType,
            FullType = BuildFullType(dataType, udtName, maxLength, precision, scale, udtSchema),
            MaxLength = maxLength,
            Precision = precision,
            Scale = scale,
            IsNullable = reader.GetString(8) == "YES",
            DefaultExpression = reader.IsDBNull(9) ? null : reader.GetString(9),
            OrdinalPosition = reader.GetInt32(10),
            IsIdentity = reader.GetString(11) == "YES",
            IdentityGeneration = reader.IsDBNull(12) ? null : reader.GetString(12),
            IsGenerated = reader.GetString(13) != "NEVER",
            GenerationExpression = reader.IsDBNull(14) ? null : reader.GetString(14),
            Collation = reader.IsDBNull(15) ? null : reader.GetString(15),
            UdtName = udtName,
            UdtSchema = udtSchema
        };
    }

    /// <summary>
    /// Builds the full type string with modifiers (e.g., "character varying(255)").
    /// Schema-qualifies USER-DEFINED types when they are outside the "public" schema.
    /// </summary>
    internal static string BuildFullType(
        string dataType, string? udtName, int? maxLength, int? precision, int? scale,
        string? udtSchema = null)
    {
        // USER-DEFINED types (enums, composites, domains) — use the UDT name, schema-qualified
        if (dataType.Equals("USER-DEFINED", StringComparison.OrdinalIgnoreCase) && udtName != null)
        {
            var effectiveSchema = udtSchema ?? "public";
            if (effectiveSchema.Equals("public", StringComparison.OrdinalIgnoreCase))
                return PgIdentifierHelper.QuoteIdentifier(udtName);
            return $"{PgIdentifierHelper.QuoteIdentifier(effectiveSchema)}.{PgIdentifierHelper.QuoteIdentifier(udtName)}";
        }

        // ARRAY types — use UDT name (e.g., "_int4" → "integer[]")
        if (dataType.Equals("ARRAY", StringComparison.OrdinalIgnoreCase) && udtName != null)
            return udtName.StartsWith('_') ? udtName[1..] + "[]" : udtName + "[]";

        // character varying / character with length
        if (dataType is "character varying" && maxLength.HasValue)
            return $"character varying({maxLength.Value})";
        if (dataType is "character" && maxLength.HasValue)
            return $"character({maxLength.Value})";

        // numeric/decimal with precision and scale
        if (dataType is "numeric" or "decimal" && precision.HasValue)
            return scale.HasValue && scale.Value > 0
                ? $"numeric({precision.Value},{scale.Value})"
                : $"numeric({precision.Value})";

        // bit / bit varying with length
        if (dataType is "bit" && maxLength.HasValue)
            return $"bit({maxLength.Value})";
        if (dataType is "bit varying" && maxLength.HasValue)
            return $"bit varying({maxLength.Value})";

        return dataType;
    }

    internal static async Task ReadPrimaryKeysAsync(
        NpgsqlConnection connection,
        Dictionary<string, PgTableDefinition> tableMap,
        CancellationToken ct)
    {
        const string sql = """
            SELECT tc.table_schema, tc.table_name, tc.constraint_name, kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
              ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
            WHERE tc.constraint_type = 'PRIMARY KEY'
              AND tc.table_schema NOT IN ('pg_catalog', 'information_schema')
            ORDER BY tc.table_schema, tc.table_name, kcu.ordinal_position
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var key = $"{reader.GetString(0)}.{reader.GetString(1)}";
            if (!tableMap.TryGetValue(key, out var table)) continue;

            table.PrimaryKey ??= new PgPrimaryKeyDefinition
            {
                Name = reader.GetString(2)
            };
            table.PrimaryKey.Columns.Add(reader.GetString(3));
        }
    }

    internal static async Task ReadForeignKeysAsync(
        NpgsqlConnection connection,
        Dictionary<string, PgTableDefinition> tableMap,
        CancellationToken ct)
    {
        // Use pg_constraint with unnest to get positionally-correct column pairs,
        // avoiding the Cartesian product from information_schema joins on composite FKs.
        const string sql = """
            SELECT n.nspname AS table_schema, c.relname AS table_name, con.conname AS constraint_name,
                   a_src.attname AS column_name, rn.nspname AS ref_schema, rc.relname AS ref_table,
                   a_ref.attname AS ref_column, con.confdeltype, con.confupdtype
            FROM pg_constraint con
            JOIN pg_class c ON con.conrelid = c.oid
            JOIN pg_namespace n ON c.relnamespace = n.oid
            JOIN pg_class rc ON con.confrelid = rc.oid
            JOIN pg_namespace rn ON rc.relnamespace = rn.oid
            CROSS JOIN LATERAL unnest(con.conkey, con.confkey) WITH ORDINALITY AS u(src_attnum, ref_attnum, ord)
            JOIN pg_attribute a_src ON a_src.attrelid = con.conrelid AND a_src.attnum = u.src_attnum
            JOIN pg_attribute a_ref ON a_ref.attrelid = con.confrelid AND a_ref.attnum = u.ref_attnum
            WHERE con.contype = 'f' AND n.nspname NOT IN ('pg_catalog', 'information_schema')
            ORDER BY n.nspname, c.relname, con.conname, u.ord
            """;

        var fkMap = new Dictionary<string, PgForeignKeyDefinition>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var key = $"{reader.GetString(0)}.{reader.GetString(1)}";
            if (!tableMap.TryGetValue(key, out var table)) continue;

            var fkName = reader.GetString(2);
            var fkKey = $"{key}.{fkName}";

            if (!fkMap.TryGetValue(fkKey, out var fk))
            {
                fk = new PgForeignKeyDefinition
                {
                    Name = fkName,
                    ReferencedSchema = reader.GetString(4),
                    ReferencedTable = reader.GetString(5),
                    DeleteAction = MapFkAction(reader.GetChar(7)),
                    UpdateAction = MapFkAction(reader.GetChar(8))
                };
                fkMap[fkKey] = fk;
                table.ForeignKeys.Add(fk);
            }

            // Each row is one (source_col, ref_col) pair in ordinal position — no dedup needed
            fk.Columns.Add(reader.GetString(3));
            fk.ReferencedColumns.Add(reader.GetString(6));
        }
    }

    /// <summary>
    /// Maps pg_constraint FK action characters to SQL standard action strings.
    /// </summary>
    internal static string MapFkAction(char action) => action switch
    {
        'a' => "NO ACTION",
        'r' => "RESTRICT",
        'c' => "CASCADE",
        'n' => "SET NULL",
        'd' => "SET DEFAULT",
        _ => "NO ACTION"
    };

    internal static async Task ReadUniqueConstraintsAsync(
        NpgsqlConnection connection,
        Dictionary<string, PgTableDefinition> tableMap,
        CancellationToken ct)
    {
        const string sql = """
            SELECT tc.table_schema, tc.table_name, tc.constraint_name, kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
              ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
            WHERE tc.constraint_type = 'UNIQUE'
              AND tc.table_schema NOT IN ('pg_catalog', 'information_schema')
            ORDER BY tc.table_schema, tc.table_name, kcu.ordinal_position
            """;

        var uqMap = new Dictionary<string, PgUniqueConstraintDefinition>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var key = $"{reader.GetString(0)}.{reader.GetString(1)}";
            if (!tableMap.TryGetValue(key, out var table)) continue;

            var uqName = reader.GetString(2);
            var uqKey = $"{key}.{uqName}";

            if (!uqMap.TryGetValue(uqKey, out var uq))
            {
                uq = new PgUniqueConstraintDefinition { Name = uqName };
                uqMap[uqKey] = uq;
                table.UniqueConstraints.Add(uq);
            }

            uq.Columns.Add(reader.GetString(3));
        }
    }

    internal static async Task ReadCheckConstraintsAsync(
        NpgsqlConnection connection,
        Dictionary<string, PgTableDefinition> tableMap,
        CancellationToken ct)
    {
        const string sql = """
            SELECT tc.table_schema, tc.table_name, tc.constraint_name, cc.check_clause
            FROM information_schema.table_constraints tc
            JOIN information_schema.check_constraints cc
              ON tc.constraint_name = cc.constraint_name AND tc.constraint_schema = cc.constraint_schema
            WHERE tc.constraint_type = 'CHECK'
              AND tc.table_schema NOT IN ('pg_catalog', 'information_schema')
              AND cc.check_clause NOT LIKE '%IS NOT NULL%'
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var key = $"{reader.GetString(0)}.{reader.GetString(1)}";
            if (!tableMap.TryGetValue(key, out var table)) continue;

            table.CheckConstraints.Add(new PgCheckConstraintDefinition
            {
                Name = reader.GetString(2),
                Expression = reader.GetString(3)
            });
        }
    }

    internal static async Task ReadIndexesAsync(
        NpgsqlConnection connection,
        Dictionary<string, PgTableDefinition> tableMap,
        CancellationToken ct)
    {
        // First, collect constraint-backing index names to skip
        const string constraintSql = """
            SELECT tc.table_schema, tc.table_name, tc.constraint_name
            FROM information_schema.table_constraints tc
            WHERE tc.constraint_type IN ('PRIMARY KEY', 'UNIQUE')
              AND tc.table_schema NOT IN ('pg_catalog', 'information_schema')
            """;

        var constraintIndexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var constraintCmd = new NpgsqlCommand(constraintSql, connection))
        await using (var constraintReader = await constraintCmd.ExecuteReaderAsync(ct))
        {
            while (await constraintReader.ReadAsync(ct))
            {
                constraintIndexes.Add(constraintReader.GetString(2));
            }
        }

        // Now read all indexes
        const string sql = """
            SELECT schemaname, tablename, indexname, indexdef
            FROM pg_indexes
            WHERE schemaname NOT IN ('pg_catalog', 'information_schema', 'pg_toast')
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var schema = reader.GetString(0);
            var tableName = reader.GetString(1);
            var indexName = reader.GetString(2);
            var indexDef = reader.GetString(3);

            // Skip indexes that back PK or UNIQUE constraints
            if (constraintIndexes.Contains(indexName))
                continue;

            var key = $"{schema}.{tableName}";
            if (!tableMap.TryGetValue(key, out var table)) continue;

            var index = ParseIndexDefinition(indexName, indexDef);
            table.Indexes.Add(index);
        }
    }

    /// <summary>
    /// Parses a CREATE INDEX DDL string to extract columns, uniqueness, access method, and filter.
    /// Falls back to storing the raw DDL when parsing fails.
    /// </summary>
    internal static PgIndexDefinition ParseIndexDefinition(string indexName, string indexDef)
    {
        var index = new PgIndexDefinition
        {
            Name = indexName,
            RawDdl = indexDef
        };

        // Determine uniqueness
        index.IsUnique = indexDef.StartsWith("CREATE UNIQUE", StringComparison.OrdinalIgnoreCase);

        // Extract access method: USING btree|hash|gin|gist|...
        var usingIdx = indexDef.IndexOf(" USING ", StringComparison.OrdinalIgnoreCase);
        if (usingIdx >= 0)
        {
            var afterUsing = indexDef[(usingIdx + 7)..];
            var spaceIdx = afterUsing.IndexOf(' ');
            if (spaceIdx > 0)
                index.AccessMethod = afterUsing[..spaceIdx].Trim();
        }

        // Extract column list from parentheses after USING method
        var parenStart = indexDef.IndexOf('(');
        if (parenStart >= 0)
        {
            var parenEnd = FindMatchingParen(indexDef, parenStart);
            if (parenEnd > parenStart)
            {
                var colSection = indexDef[(parenStart + 1)..parenEnd];
                index.Columns = colSection
                    .Split(',')
                    .Select(c => c.Trim())
                    .Where(c => !string.IsNullOrEmpty(c))
                    .ToList();
            }
        }

        // Extract WHERE clause (partial index filter)
        var whereIdx = indexDef.IndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase);
        if (whereIdx >= 0)
        {
            index.FilterExpression = indexDef[(whereIdx + 7)..].TrimEnd(';').Trim();
        }

        return index;
    }

    private static int FindMatchingParen(string text, int openIndex)
    {
        var depth = 0;
        for (var i = openIndex; i < text.Length; i++)
        {
            if (text[i] == '(') depth++;
            else if (text[i] == ')') depth--;
            if (depth == 0) return i;
        }
        return -1;
    }

    internal static async Task<List<PgSequenceDefinition>> ReadSequencesAsync(
        NpgsqlConnection connection, CancellationToken ct)
    {
        const string sql = """
            SELECT n.nspname AS schema, s.relname AS name,
                   seq.seqtypid::regtype AS data_type,
                   seq.seqstart, seq.seqincrement, seq.seqmin, seq.seqmax, seq.seqcycle,
                   dep_n.nspname || '.' || dep_t.relname || '.' || a.attname AS owned_by
            FROM pg_class s
            JOIN pg_namespace n ON s.relnamespace = n.oid
            JOIN pg_sequence seq ON seq.seqrelid = s.oid
            LEFT JOIN pg_depend d ON d.objid = s.oid AND d.deptype = 'a'
            LEFT JOIN pg_class dep_t ON d.refobjid = dep_t.oid
            LEFT JOIN pg_namespace dep_n ON dep_t.relnamespace = dep_n.oid
            LEFT JOIN pg_attribute a ON a.attrelid = d.refobjid AND a.attnum = d.refobjsubid
            WHERE s.relkind = 'S'
              AND n.nspname NOT IN ('pg_catalog', 'information_schema')
            """;

        var sequences = new List<PgSequenceDefinition>();
        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            sequences.Add(new PgSequenceDefinition
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                DataType = reader.GetString(2),
                StartValue = reader.GetInt64(3),
                IncrementBy = reader.GetInt64(4),
                MinValue = reader.IsDBNull(5) ? null : reader.GetInt64(5),
                MaxValue = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                IsCyclic = reader.GetBoolean(7),
                OwnedBy = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }

        return sequences;
    }

    internal static async Task<List<PgViewDefinition>> ReadViewsAsync(
        NpgsqlConnection connection, CancellationToken ct)
    {
        const string sql = """
            SELECT schemaname, viewname, definition, false AS is_materialized
            FROM pg_views
            WHERE schemaname NOT IN ('pg_catalog', 'information_schema', 'pg_toast')
            UNION ALL
            SELECT schemaname, matviewname, definition, true AS is_materialized
            FROM pg_matviews
            WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
            """;

        var views = new List<PgViewDefinition>();
        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            views.Add(new PgViewDefinition
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                Definition = reader.GetString(2),
                IsMaterialized = reader.GetBoolean(3)
            });
        }

        return views;
    }

    internal static async Task<List<PgFunctionDefinition>> ReadFunctionsAsync(
        NpgsqlConnection connection, CancellationToken ct)
    {
        const string sql = """
            SELECT n.nspname AS schema, p.proname AS name,
                   pg_get_functiondef(p.oid) AS definition,
                   l.lanname AS language,
                   p.prokind AS kind
            FROM pg_proc p
            JOIN pg_namespace n ON p.pronamespace = n.oid
            JOIN pg_language l ON p.prolang = l.oid
            WHERE n.nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast')
              AND p.prokind IN ('f', 'p')
            """;

        var functions = new List<PgFunctionDefinition>();
        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            functions.Add(new PgFunctionDefinition
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                Definition = reader.GetString(2),
                Language = reader.GetString(3),
                Kind = reader.GetString(4)
            });
        }

        return functions;
    }

    /// <summary>
    /// Checks whether a table is in the inclusion list.
    /// Supports both "schema.table" and plain "table" formats.
    /// </summary>
    internal static bool IsObjectIncluded(string schema, string tableName, IReadOnlyList<string> includedObjects)
    {
        foreach (var included in includedObjects)
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