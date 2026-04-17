using Scaffold.Migration.PostgreSql;
using Scaffold.Migration.PostgreSql.Models;

namespace Scaffold.Migration.Tests.PostgreSql;

public class PostgreSqlDdlGeneratorTests
{
    private readonly PostgreSqlDdlGenerator _generator = new();

    #region Helpers

    private static PgTableDefinition SimpleTable(
        string name = "users", string schema = "public") => new()
    {
        Schema = schema,
        TableName = name,
        Columns =
        [
            new PgColumnDefinition
            {
                Name = "id", DataType = "integer", FullType = "integer",
                IsNullable = false, IsIdentity = true, IdentityGeneration = "BY DEFAULT",
                OrdinalPosition = 1
            },
            new PgColumnDefinition
            {
                Name = "name", DataType = "character varying", FullType = "character varying(100)",
                IsNullable = false, OrdinalPosition = 2
            },
            new PgColumnDefinition
            {
                Name = "email", DataType = "character varying", FullType = "character varying(255)",
                IsNullable = true, OrdinalPosition = 3
            }
        ],
        PrimaryKey = new PgPrimaryKeyDefinition { Name = "users_pkey", Columns = ["id"] }
    };

    private static PgTableDefinition OrdersTable() => new()
    {
        Schema = "public",
        TableName = "orders",
        Columns =
        [
            new PgColumnDefinition
            {
                Name = "id", DataType = "integer", FullType = "integer",
                IsNullable = false, IsIdentity = true, IdentityGeneration = "ALWAYS",
                OrdinalPosition = 1
            },
            new PgColumnDefinition
            {
                Name = "user_id", DataType = "integer", FullType = "integer",
                IsNullable = false, OrdinalPosition = 2
            },
            new PgColumnDefinition
            {
                Name = "amount", DataType = "numeric", FullType = "numeric(18,2)",
                IsNullable = false, OrdinalPosition = 3
            },
            new PgColumnDefinition
            {
                Name = "created_at", DataType = "timestamp with time zone",
                FullType = "timestamp with time zone",
                IsNullable = false, DefaultExpression = "now()", OrdinalPosition = 4
            }
        ],
        PrimaryKey = new PgPrimaryKeyDefinition { Name = "orders_pkey", Columns = ["id"] },
        ForeignKeys =
        [
            new PgForeignKeyDefinition
            {
                Name = "fk_orders_users",
                ReferencedSchema = "public",
                ReferencedTable = "users",
                Columns = ["user_id"],
                ReferencedColumns = ["id"],
                DeleteAction = "CASCADE",
                UpdateAction = "NO ACTION"
            }
        ]
    };

    #endregion

    #region GenerateCreateEnum

    [Fact]
    public void GenerateCreateEnum_SimpleEnum_ProducesValidDdl()
    {
        var enumType = new PgEnumTypeDefinition
        {
            Schema = "public",
            Name = "mood",
            Labels = ["happy", "sad", "neutral"]
        };

        var ddl = _generator.GenerateCreateEnum(enumType);

        Assert.Contains("CREATE TYPE", ddl);
        Assert.Contains("\"public\".\"mood\"", ddl);
        Assert.Contains("AS ENUM", ddl);
        Assert.Contains("'happy'", ddl);
        Assert.Contains("'sad'", ddl);
        Assert.Contains("'neutral'", ddl);
        Assert.EndsWith(";", ddl);
    }

    [Fact]
    public void GenerateCreateEnum_LabelWithQuote_EscapesCorrectly()
    {
        var enumType = new PgEnumTypeDefinition
        {
            Schema = "public",
            Name = "status",
            Labels = ["it's active", "inactive"]
        };

        var ddl = _generator.GenerateCreateEnum(enumType);

        Assert.Contains("'it''s active'", ddl);
    }

    [Fact]
    public void GenerateCreateEnum_CustomSchema_UsesSchemaPrefix()
    {
        var enumType = new PgEnumTypeDefinition
        {
            Schema = "myschema",
            Name = "priority",
            Labels = ["low", "medium", "high"]
        };

        var ddl = _generator.GenerateCreateEnum(enumType);

        Assert.Contains("\"myschema\".\"priority\"", ddl);
    }

    #endregion

    #region GenerateCreateSequence

    [Fact]
    public void GenerateCreateSequence_DefaultOptions_ProducesValidDdl()
    {
        var seq = new PgSequenceDefinition
        {
            Schema = "public",
            Name = "users_id_seq",
            DataType = "bigint",
            StartValue = 1,
            IncrementBy = 1
        };

        var ddl = _generator.GenerateCreateSequence(seq);

        Assert.Contains("CREATE SEQUENCE", ddl);
        Assert.Contains("\"public\".\"users_id_seq\"", ddl);
        Assert.Contains("AS bigint", ddl);
        Assert.Contains("START WITH 1", ddl);
        Assert.Contains("INCREMENT BY 1", ddl);
        Assert.Contains("NO MINVALUE", ddl);
        Assert.Contains("NO MAXVALUE", ddl);
        Assert.Contains("NO CYCLE", ddl);
        Assert.EndsWith(";", ddl);
    }

    [Fact]
    public void GenerateCreateSequence_WithCycle_IncludesCycle()
    {
        var seq = new PgSequenceDefinition
        {
            Schema = "public",
            Name = "cyclic_seq",
            DataType = "integer",
            StartValue = 1,
            IncrementBy = 1,
            MinValue = 1,
            MaxValue = 100,
            IsCyclic = true
        };

        var ddl = _generator.GenerateCreateSequence(seq);

        Assert.Contains("MINVALUE 1", ddl);
        Assert.Contains("MAXVALUE 100", ddl);
        Assert.Contains(" CYCLE", ddl);
        Assert.DoesNotContain("NO CYCLE", ddl);
    }

    [Fact]
    public void GenerateCreateSequence_WithOwnedBy_IncludesOwnedBy()
    {
        var seq = new PgSequenceDefinition
        {
            Schema = "public",
            Name = "users_id_seq",
            DataType = "bigint",
            StartValue = 1,
            IncrementBy = 1,
            OwnedBy = "public.users.id"
        };

        var ddl = _generator.GenerateCreateSequence(seq);

        Assert.Contains("OWNED BY \"public\".\"users\".\"id\"", ddl);
    }

    #endregion

    #region GenerateCreateTable

    [Fact]
    public void GenerateCreateTable_SimpleTable_ContainsColumnsAndPK()
    {
        var table = SimpleTable();

        var ddl = _generator.GenerateCreateTable(table);

        Assert.Contains("CREATE TABLE \"public\".\"users\"", ddl);
        Assert.Contains("\"id\" integer GENERATED BY DEFAULT AS IDENTITY", ddl);
        Assert.Contains("\"name\" character varying(100)", ddl);
        Assert.Contains("NOT NULL", ddl);
        Assert.Contains("CONSTRAINT \"users_pkey\" PRIMARY KEY (\"id\")", ddl);
        Assert.EndsWith(");", ddl);
    }

    [Fact]
    public void GenerateCreateTable_WithDefault_IncludesDefault()
    {
        var table = new PgTableDefinition
        {
            Schema = "public",
            TableName = "events",
            Columns =
            [
                new PgColumnDefinition
                {
                    Name = "created_at", DataType = "timestamp with time zone",
                    FullType = "timestamp with time zone",
                    IsNullable = false, DefaultExpression = "now()", OrdinalPosition = 1
                }
            ]
        };

        var ddl = _generator.GenerateCreateTable(table);

        Assert.Contains("DEFAULT now()", ddl);
    }

    [Fact]
    public void GenerateCreateTable_WithGeneratedColumn_IncludesGeneratedClause()
    {
        var table = new PgTableDefinition
        {
            Schema = "public",
            TableName = "products",
            Columns =
            [
                new PgColumnDefinition
                {
                    Name = "price", DataType = "numeric", FullType = "numeric(10,2)",
                    IsNullable = false, OrdinalPosition = 1
                },
                new PgColumnDefinition
                {
                    Name = "tax", DataType = "numeric", FullType = "numeric(10,2)",
                    IsNullable = false, OrdinalPosition = 2
                },
                new PgColumnDefinition
                {
                    Name = "total", DataType = "numeric", FullType = "numeric(10,2)",
                    IsNullable = false, IsGenerated = true,
                    GenerationExpression = "price + tax", OrdinalPosition = 3
                }
            ]
        };

        var ddl = _generator.GenerateCreateTable(table);

        Assert.Contains("GENERATED ALWAYS AS (price + tax) STORED", ddl);
    }

    [Fact]
    public void GenerateCreateTable_WithIdentityAlways_IncludesAlways()
    {
        var table = new PgTableDefinition
        {
            Schema = "public",
            TableName = "audit_log",
            Columns =
            [
                new PgColumnDefinition
                {
                    Name = "id", DataType = "bigint", FullType = "bigint",
                    IsNullable = false, IsIdentity = true, IdentityGeneration = "ALWAYS",
                    OrdinalPosition = 1
                }
            ]
        };

        var ddl = _generator.GenerateCreateTable(table);

        Assert.Contains("GENERATED ALWAYS AS IDENTITY", ddl);
    }

    [Fact]
    public void GenerateCreateTable_WithUniqueConstraint_IncludesUnique()
    {
        var table = SimpleTable();
        table.UniqueConstraints.Add(new PgUniqueConstraintDefinition
        {
            Name = "uq_users_email",
            Columns = ["email"]
        });

        var ddl = _generator.GenerateCreateTable(table);

        Assert.Contains("CONSTRAINT \"uq_users_email\" UNIQUE (\"email\")", ddl);
    }

    [Fact]
    public void GenerateCreateTable_WithCheckConstraint_IncludesCheck()
    {
        var table = SimpleTable();
        table.CheckConstraints.Add(new PgCheckConstraintDefinition
        {
            Name = "chk_name_length",
            Expression = "length(name) > 0"
        });

        var ddl = _generator.GenerateCreateTable(table);

        Assert.Contains("CONSTRAINT \"chk_name_length\" CHECK (length(name) > 0)", ddl);
    }

    [Fact]
    public void GenerateCreateTable_WithCollation_IncludesCollate()
    {
        var table = new PgTableDefinition
        {
            Schema = "public",
            TableName = "localized",
            Columns =
            [
                new PgColumnDefinition
                {
                    Name = "name", DataType = "text", FullType = "text",
                    IsNullable = true, OrdinalPosition = 1,
                    Collation = "en_US.utf8"
                }
            ]
        };

        var ddl = _generator.GenerateCreateTable(table);

        Assert.Contains("COLLATE \"en_US.utf8\"", ddl);
    }

    [Fact]
    public void GenerateCreateTable_NullableColumn_OmitsNotNull()
    {
        var table = new PgTableDefinition
        {
            Schema = "public",
            TableName = "test",
            Columns =
            [
                new PgColumnDefinition
                {
                    Name = "notes", DataType = "text", FullType = "text",
                    IsNullable = true, OrdinalPosition = 1
                }
            ]
        };

        var ddl = _generator.GenerateCreateTable(table);

        Assert.DoesNotContain("NOT NULL", ddl);
    }

    #endregion

    #region GenerateAddForeignKey

    [Fact]
    public void GenerateAddForeignKey_Cascade_ProducesValidDdl()
    {
        var table = OrdersTable();
        var fk = table.ForeignKeys[0];

        var ddl = _generator.GenerateAddForeignKey(table, fk);

        Assert.Contains("ALTER TABLE \"public\".\"orders\"", ddl);
        Assert.Contains("ADD CONSTRAINT \"fk_orders_users\"", ddl);
        Assert.Contains("FOREIGN KEY (\"user_id\")", ddl);
        Assert.Contains("REFERENCES \"public\".\"users\" (\"id\")", ddl);
        Assert.Contains("ON DELETE CASCADE", ddl);
        Assert.Contains("ON UPDATE NO ACTION", ddl);
        Assert.EndsWith(";", ddl);
    }

    [Theory]
    [InlineData("CASCADE", "CASCADE")]
    [InlineData("SET NULL", "SET NULL")]
    [InlineData("SET DEFAULT", "SET DEFAULT")]
    [InlineData("RESTRICT", "RESTRICT")]
    [InlineData("NO ACTION", "NO ACTION")]
    public void GenerateAddForeignKey_ReferentialActions_ProducesCorrectAction(
        string action, string expected)
    {
        var table = new PgTableDefinition { Schema = "public", TableName = "child" };
        var fk = new PgForeignKeyDefinition
        {
            Name = "fk_test",
            ReferencedSchema = "public",
            ReferencedTable = "parent",
            Columns = ["parent_id"],
            ReferencedColumns = ["id"],
            DeleteAction = action,
            UpdateAction = action
        };

        var ddl = _generator.GenerateAddForeignKey(table, fk);

        Assert.Contains($"ON DELETE {expected}", ddl);
        Assert.Contains($"ON UPDATE {expected}", ddl);
    }

    [Fact]
    public void GenerateAddForeignKey_InvalidAction_DefaultsToNoAction()
    {
        var table = new PgTableDefinition { Schema = "public", TableName = "child" };
        var fk = new PgForeignKeyDefinition
        {
            Name = "fk_test",
            ReferencedSchema = "public",
            ReferencedTable = "parent",
            Columns = ["parent_id"],
            ReferencedColumns = ["id"],
            DeleteAction = "INVALID",
            UpdateAction = "INVALID"
        };

        var ddl = _generator.GenerateAddForeignKey(table, fk);

        Assert.Contains("ON DELETE NO ACTION", ddl);
        Assert.Contains("ON UPDATE NO ACTION", ddl);
    }

    [Fact]
    public void GenerateAddForeignKey_MultiColumn_ListsAllColumns()
    {
        var table = new PgTableDefinition { Schema = "public", TableName = "child" };
        var fk = new PgForeignKeyDefinition
        {
            Name = "fk_composite",
            ReferencedSchema = "public",
            ReferencedTable = "parent",
            Columns = ["col_a", "col_b"],
            ReferencedColumns = ["ref_a", "ref_b"],
            DeleteAction = "NO ACTION",
            UpdateAction = "NO ACTION"
        };

        var ddl = _generator.GenerateAddForeignKey(table, fk);

        Assert.Contains("FOREIGN KEY (\"col_a\", \"col_b\")", ddl);
        Assert.Contains("(\"ref_a\", \"ref_b\")", ddl);
    }

    #endregion

    #region GenerateCreateIndex

    [Fact]
    public void GenerateCreateIndex_WithRawDdl_UsesRawDdlDirectly()
    {
        var table = SimpleTable();
        var index = new PgIndexDefinition
        {
            Name = "idx_users_email",
            Columns = ["email"],
            RawDdl = "CREATE INDEX idx_users_email ON public.users USING btree (email)"
        };

        var ddl = _generator.GenerateCreateIndex(table, index);

        Assert.Equal("CREATE INDEX idx_users_email ON public.users USING btree (email);", ddl);
    }

    [Fact]
    public void GenerateCreateIndex_WithRawDdlSemicolon_DoesNotDoubleSemicolon()
    {
        var table = SimpleTable();
        var index = new PgIndexDefinition
        {
            Name = "idx_test",
            RawDdl = "CREATE INDEX idx_test ON public.users (email);"
        };

        var ddl = _generator.GenerateCreateIndex(table, index);

        Assert.Equal("CREATE INDEX idx_test ON public.users (email);", ddl);
        Assert.DoesNotContain(";;", ddl);
    }

    [Fact]
    public void GenerateCreateIndex_NoRawDdl_ConstructsFromParts()
    {
        var table = SimpleTable();
        var index = new PgIndexDefinition
        {
            Name = "idx_users_email",
            Columns = ["email"],
            IsUnique = false,
            AccessMethod = "btree"
        };

        var ddl = _generator.GenerateCreateIndex(table, index);

        Assert.Contains("CREATE INDEX \"idx_users_email\"", ddl);
        Assert.Contains("ON \"public\".\"users\"", ddl);
        Assert.Contains("(\"email\")", ddl);
        Assert.DoesNotContain("USING", ddl); // btree is default, should be omitted
        Assert.EndsWith(";", ddl);
    }

    [Fact]
    public void GenerateCreateIndex_UniqueNoRawDdl_IncludesUnique()
    {
        var table = SimpleTable();
        var index = new PgIndexDefinition
        {
            Name = "idx_unique_email",
            Columns = ["email"],
            IsUnique = true,
            AccessMethod = "btree"
        };

        var ddl = _generator.GenerateCreateIndex(table, index);

        Assert.Contains("CREATE UNIQUE INDEX", ddl);
    }

    [Fact]
    public void GenerateCreateIndex_NonBtreeMethod_IncludesUsing()
    {
        var table = SimpleTable();
        var index = new PgIndexDefinition
        {
            Name = "idx_gin",
            Columns = ["tags"],
            AccessMethod = "gin"
        };

        var ddl = _generator.GenerateCreateIndex(table, index);

        Assert.Contains("USING gin", ddl);
    }

    [Fact]
    public void GenerateCreateIndex_WithIncludedColumns_IncludesInclude()
    {
        var table = SimpleTable();
        var index = new PgIndexDefinition
        {
            Name = "idx_covering",
            Columns = ["name"],
            IncludedColumns = ["email"],
            AccessMethod = "btree"
        };

        var ddl = _generator.GenerateCreateIndex(table, index);

        Assert.Contains("INCLUDE (\"email\")", ddl);
    }

    [Fact]
    public void GenerateCreateIndex_WithFilter_IncludesWhere()
    {
        var table = SimpleTable();
        var index = new PgIndexDefinition
        {
            Name = "idx_active",
            Columns = ["email"],
            AccessMethod = "btree",
            FilterExpression = "is_active = true"
        };

        var ddl = _generator.GenerateCreateIndex(table, index);

        Assert.Contains("WHERE is_active = true", ddl);
    }

    #endregion

    #region GenerateCreateView

    [Fact]
    public void GenerateCreateView_RegularView_ProducesCreateOrReplace()
    {
        var view = new PgViewDefinition
        {
            Schema = "public",
            Name = "active_users",
            Definition = " SELECT id, name FROM users WHERE is_active = true",
            IsMaterialized = false
        };

        var ddl = _generator.GenerateCreateView(view);

        Assert.Contains("CREATE OR REPLACE VIEW", ddl);
        Assert.Contains("\"public\".\"active_users\"", ddl);
        Assert.Contains("SELECT id, name FROM users WHERE is_active = true", ddl);
        Assert.EndsWith(";", ddl);
    }

    [Fact]
    public void GenerateCreateView_MaterializedView_ProducesCreateMaterialized()
    {
        var view = new PgViewDefinition
        {
            Schema = "public",
            Name = "summary_stats",
            Definition = " SELECT count(*) FROM users",
            IsMaterialized = true
        };

        var ddl = _generator.GenerateCreateView(view);

        Assert.Contains("CREATE MATERIALIZED VIEW", ddl);
        Assert.DoesNotContain("OR REPLACE", ddl);
        Assert.Contains("\"public\".\"summary_stats\"", ddl);
        Assert.EndsWith(";", ddl);
    }

    [Fact]
    public void GenerateCreateView_DefinitionWithSemicolon_DoesNotDoubleSemicolon()
    {
        var view = new PgViewDefinition
        {
            Schema = "public",
            Name = "test_view",
            Definition = " SELECT 1;",
            IsMaterialized = false
        };

        var ddl = _generator.GenerateCreateView(view);

        Assert.DoesNotContain(";;", ddl);
        Assert.EndsWith(";", ddl);
    }

    #endregion

    #region GenerateCreateFunction

    [Fact]
    public void GenerateCreateFunction_PassesThroughDefinition()
    {
        var function = new PgFunctionDefinition
        {
            Schema = "public",
            Name = "add_numbers",
            Definition = "CREATE OR REPLACE FUNCTION public.add_numbers(a integer, b integer)\nRETURNS integer\nLANGUAGE plpgsql\nAS $function$\nBEGIN\n  RETURN a + b;\nEND;\n$function$",
            Language = "plpgsql",
            Kind = "f"
        };

        var ddl = _generator.GenerateCreateFunction(function);

        Assert.StartsWith("CREATE OR REPLACE FUNCTION", ddl);
        Assert.Contains("add_numbers", ddl);
        Assert.Contains("RETURN a + b;", ddl);
    }

    [Fact]
    public void GenerateCreateFunction_NoTrailingSemicolon_AddsSemicolon()
    {
        var function = new PgFunctionDefinition
        {
            Schema = "public",
            Name = "get_one",
            Definition = "CREATE OR REPLACE FUNCTION public.get_one()\nRETURNS integer\nLANGUAGE sql\nAS $function$ SELECT 1 $function$",
            Language = "sql",
            Kind = "f"
        };

        var ddl = _generator.GenerateCreateFunction(function);

        Assert.EndsWith(";", ddl);
    }

    #endregion

    #region GenerateDdl — Ordering

    [Fact]
    public void GenerateDdl_ProducesCorrectOrdering()
    {
        var snapshot = new PgSchemaSnapshot
        {
            EnumTypes =
            [
                new PgEnumTypeDefinition
                {
                    Schema = "public", Name = "status",
                    Labels = ["active", "inactive"]
                }
            ],
            Sequences =
            [
                new PgSequenceDefinition
                {
                    Schema = "public", Name = "users_id_seq",
                    DataType = "bigint", StartValue = 1, IncrementBy = 1
                }
            ],
            Tables = [SimpleTable(), OrdersTable()],
            Views =
            [
                new PgViewDefinition
                {
                    Schema = "public", Name = "active_users",
                    Definition = " SELECT * FROM users"
                }
            ],
            Functions =
            [
                new PgFunctionDefinition
                {
                    Schema = "public", Name = "get_user",
                    Definition = "CREATE OR REPLACE FUNCTION public.get_user() RETURNS void LANGUAGE sql AS $$ SELECT 1 $$"
                }
            ]
        };

        var ddl = _generator.GenerateDdl(snapshot);

        // Find positions of each category
        var enumIdx = ddl.FindIndex(s => s.Contains("CREATE TYPE"));
        var seqIdx = ddl.FindIndex(s => s.Contains("CREATE SEQUENCE"));
        var tableIdx = ddl.FindIndex(s => s.Contains("CREATE TABLE"));
        var fkIdx = ddl.FindIndex(s => s.Contains("ALTER TABLE"));
        var viewIdx = ddl.FindIndex(s => s.Contains("CREATE OR REPLACE VIEW"));
        var funcIdx = ddl.FindIndex(s => s.Contains("CREATE OR REPLACE FUNCTION"));

        Assert.True(enumIdx >= 0, "Should contain enum DDL");
        Assert.True(seqIdx >= 0, "Should contain sequence DDL");
        Assert.True(tableIdx >= 0, "Should contain table DDL");
        Assert.True(fkIdx >= 0, "Should contain FK DDL");
        Assert.True(viewIdx >= 0, "Should contain view DDL");
        Assert.True(funcIdx >= 0, "Should contain function DDL");

        // Order: enum < sequence < table < FK < view < function
        Assert.True(enumIdx < seqIdx, "Enums should come before sequences");
        Assert.True(seqIdx < tableIdx, "Sequences should come before tables");
        Assert.True(tableIdx < fkIdx, "Tables should come before FKs");
        Assert.True(fkIdx < viewIdx, "FKs should come before views");
        Assert.True(viewIdx < funcIdx, "Views should come before functions");
    }

    [Fact]
    public void GenerateDdl_TablesInDependencyOrder_ParentsBeforeChildren()
    {
        var snapshot = new PgSchemaSnapshot
        {
            Tables = [OrdersTable(), SimpleTable()], // Orders first, but depends on Users
        };

        var ddl = _generator.GenerateDdl(snapshot);

        var userTableIdx = ddl.FindIndex(s => s.Contains("\"users\"") && s.Contains("CREATE TABLE"));
        var orderTableIdx = ddl.FindIndex(s => s.Contains("\"orders\"") && s.Contains("CREATE TABLE"));

        Assert.True(userTableIdx >= 0, "Should contain users table");
        Assert.True(orderTableIdx >= 0, "Should contain orders table");
        Assert.True(userTableIdx < orderTableIdx, "Users (parent) should come before orders (child)");
    }

    [Fact]
    public void GenerateDdl_EmptySnapshot_ReturnsEmptyList()
    {
        var snapshot = new PgSchemaSnapshot();

        var ddl = _generator.GenerateDdl(snapshot);

        Assert.Empty(ddl);
    }

    #endregion

    #region Identifier Quoting

    [Fact]
    public void GenerateCreateTable_SpecialCharacterNames_QuotesCorrectly()
    {
        var table = new PgTableDefinition
        {
            Schema = "my schema",
            TableName = "my-table",
            Columns =
            [
                new PgColumnDefinition
                {
                    Name = "my column", DataType = "text", FullType = "text",
                    IsNullable = true, OrdinalPosition = 1
                }
            ]
        };

        var ddl = _generator.GenerateCreateTable(table);

        Assert.Contains("\"my schema\".\"my-table\"", ddl);
        Assert.Contains("\"my column\"", ddl);
    }

    [Fact]
    public void GenerateCreateTable_NameWithDoubleQuote_EscapesCorrectly()
    {
        var table = new PgTableDefinition
        {
            Schema = "public",
            TableName = "table\"name",
            Columns =
            [
                new PgColumnDefinition
                {
                    Name = "col\"name", DataType = "text", FullType = "text",
                    IsNullable = true, OrdinalPosition = 1
                }
            ]
        };

        var ddl = _generator.GenerateCreateTable(table);

        Assert.Contains("\"table\"\"name\"", ddl);
        Assert.Contains("\"col\"\"name\"", ddl);
    }

    #endregion

    #region GenerateCreateIndex — Access Methods

    [Theory]
    [InlineData("hash")]
    [InlineData("gist")]
    [InlineData("gin")]
    [InlineData("brin")]
    public void GenerateCreateIndex_NonBtreeAccessMethods_IncludesUsing(string method)
    {
        var table = SimpleTable();
        var index = new PgIndexDefinition
        {
            Name = "idx_test",
            Columns = ["name"],
            AccessMethod = method
        };

        var ddl = _generator.GenerateCreateIndex(table, index);

        Assert.Contains($"USING {method}", ddl);
    }

    #endregion
}
