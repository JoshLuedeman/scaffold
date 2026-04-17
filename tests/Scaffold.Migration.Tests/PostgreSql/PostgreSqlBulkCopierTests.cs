using Scaffold.Core.Interfaces;
using Scaffold.Migration.PostgreSql;
using Scaffold.Migration.Shared;

namespace Scaffold.Migration.Tests.PostgreSql;

public class PostgreSqlBulkCopierTests
{
    #region CopyDataAsync — empty input

    [Fact]
    public async Task CopyDataAsync_EmptyTableList_ReturnsZero()
    {
        var copier = new PostgreSqlBulkCopier();
        var result = await copier.CopyDataAsync(
            "Host=fake;Database=source",
            "Host=fake;Database=target",
            Array.Empty<string>());

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CopyDataAsync_EmptyTableList_ReportsCompletionProgress()
    {
        var copier = new PostgreSqlBulkCopier();
        var reports = new List<MigrationProgress>();
        var progress = new SynchronousProgress<MigrationProgress>(reports.Add);

        await copier.CopyDataAsync(
            "Host=fake;Database=source",
            "Host=fake;Database=target",
            Array.Empty<string>(),
            progress);

        Assert.Single(reports);
        Assert.Equal("DataMigration", reports[0].Phase);
        Assert.Equal(100, reports[0].PercentComplete);
        Assert.Equal("No tables to migrate.", reports[0].Message);
    }

    [Fact]
    public async Task CopyDataAsync_EmptyTableList_NullProgress_DoesNotThrow()
    {
        var copier = new PostgreSqlBulkCopier();

        // Should not throw even with null progress
        var result = await copier.CopyDataAsync(
            "Host=fake;Database=source",
            "Host=fake;Database=target",
            Array.Empty<string>(),
            progress: null);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CopyDataAsync_EmptyTableList_WithTimeout_ReturnsZero()
    {
        var copier = new PostgreSqlBulkCopier();
        var result = await copier.CopyDataAsync(
            "Host=fake;Database=source",
            "Host=fake;Database=target",
            Array.Empty<string>(),
            bulkCopyTimeout: 120);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CopyDataAsync_EmptyTableList_WithCancellation_ReturnsZero()
    {
        var copier = new PostgreSqlBulkCopier();
        using var cts = new CancellationTokenSource();

        var result = await copier.CopyDataAsync(
            "Host=fake;Database=source",
            "Host=fake;Database=target",
            Array.Empty<string>(),
            ct: cts.Token);

        Assert.Equal(0, result);
    }

    #endregion

    #region BuildCopyExportCommand — COPY TO STDOUT

    [Fact]
    public void BuildCopyExportCommand_SimpleTable_GeneratesCorrectSql()
    {
        var result = PostgreSqlBulkCopier.BuildCopyExportCommand(
            "public.users",
            new[] { "id", "name", "email" });

        Assert.Equal(
            "COPY \"public\".\"users\" (\"id\", \"name\", \"email\") TO STDOUT",
            result);
    }

    [Fact]
    public void BuildCopyExportCommand_DboSchema_MapsToPublic()
    {
        var result = PostgreSqlBulkCopier.BuildCopyExportCommand(
            "dbo.Users",
            new[] { "Id", "UserName" });

        Assert.Equal(
            "COPY \"public\".\"Users\" (\"Id\", \"UserName\") TO STDOUT",
            result);
    }

    [Fact]
    public void BuildCopyExportCommand_SingleColumn_NoTrailingComma()
    {
        var result = PostgreSqlBulkCopier.BuildCopyExportCommand(
            "public.config",
            new[] { "value" });

        Assert.Equal(
            "COPY \"public\".\"config\" (\"value\") TO STDOUT",
            result);
    }

    [Fact]
    public void BuildCopyExportCommand_ColumnWithSpecialChars_ProperlyQuoted()
    {
        var result = PostgreSqlBulkCopier.BuildCopyExportCommand(
            "public.data",
            new[] { "normal_col", "has\"quote", "has space" });

        Assert.Equal(
            "COPY \"public\".\"data\" (\"normal_col\", \"has\"\"quote\", \"has space\") TO STDOUT",
            result);
    }

    [Fact]
    public void BuildCopyExportCommand_SinglePartTableName_QuotedCorrectly()
    {
        var result = PostgreSqlBulkCopier.BuildCopyExportCommand(
            "users",
            new[] { "id", "name" });

        Assert.Equal(
            "COPY \"users\" (\"id\", \"name\") TO STDOUT",
            result);
    }

    [Fact]
    public void BuildCopyExportCommand_BracketNotation_StrippedAndQuoted()
    {
        var result = PostgreSqlBulkCopier.BuildCopyExportCommand(
            "[dbo].[Products]",
            new[] { "ProductId", "Name" });

        Assert.Equal(
            "COPY \"public\".\"Products\" (\"ProductId\", \"Name\") TO STDOUT",
            result);
    }

    #endregion

    #region BuildCopyImportCommand — COPY FROM STDIN

    [Fact]
    public void BuildCopyImportCommand_SimpleTable_GeneratesCorrectSql()
    {
        var result = PostgreSqlBulkCopier.BuildCopyImportCommand(
            "public.users",
            new[] { "id", "name", "email" });

        Assert.Equal(
            "COPY \"public\".\"users\" (\"id\", \"name\", \"email\") FROM STDIN",
            result);
    }

    [Fact]
    public void BuildCopyImportCommand_DboSchema_MapsToPublic()
    {
        var result = PostgreSqlBulkCopier.BuildCopyImportCommand(
            "dbo.Orders",
            new[] { "OrderId", "CustomerId" });

        Assert.Equal(
            "COPY \"public\".\"Orders\" (\"OrderId\", \"CustomerId\") FROM STDIN",
            result);
    }

    [Fact]
    public void BuildCopyImportCommand_ColumnWithSpecialChars_ProperlyQuoted()
    {
        var result = PostgreSqlBulkCopier.BuildCopyImportCommand(
            "public.data",
            new[] { "col with space", "col\"with\"quotes" });

        Assert.Equal(
            "COPY \"public\".\"data\" (\"col with space\", \"col\"\"with\"\"quotes\") FROM STDIN",
            result);
    }

    #endregion

    #region BuildResetSequenceSql — sequence reset query

    [Fact]
    public void BuildResetSequenceSql_SimpleNames_GeneratesCorrectSql()
    {
        var result = PostgreSqlBulkCopier.BuildResetSequenceSql(
            "public.users_id_seq",
            "public.users",
            "id");

        Assert.Equal(
            "SELECT setval('public.users_id_seq'::regclass, COALESCE((SELECT MAX(\"id\") FROM \"public\".\"users\"), 1), COALESCE((SELECT MAX(\"id\") FROM \"public\".\"users\") IS NOT NULL, false))",
            result);
    }

    [Fact]
    public void BuildResetSequenceSql_CustomSchema_PreservedCorrectly()
    {
        var result = PostgreSqlBulkCopier.BuildResetSequenceSql(
            "sales.orders_order_id_seq",
            "sales.orders",
            "order_id");

        Assert.Equal(
            "SELECT setval('sales.orders_order_id_seq'::regclass, COALESCE((SELECT MAX(\"order_id\") FROM \"sales\".\"orders\"), 1), COALESCE((SELECT MAX(\"order_id\") FROM \"sales\".\"orders\") IS NOT NULL, false))",
            result);
    }

    [Fact]
    public void BuildResetSequenceSql_ColumnWithSpecialChars_Quoted()
    {
        var result = PostgreSqlBulkCopier.BuildResetSequenceSql(
            "public.my_seq",
            "public.my_table",
            "my\"col");

        Assert.Contains("\"my\"\"col\"", result);
        Assert.StartsWith("SELECT setval(", result);
        Assert.Contains("IS NOT NULL, false)", result);
    }

    [Fact]
    public void BuildResetSequenceSql_ContainsCoalesceWithFallbackToOne()
    {
        var result = PostgreSqlBulkCopier.BuildResetSequenceSql(
            "public.seq1",
            "public.tbl1",
            "col1");

        // COALESCE ensures empty tables get sequence reset to 1
        Assert.Contains("COALESCE((SELECT MAX(\"col1\") FROM \"public\".\"tbl1\"), 1)", result);
    }

    [Fact]
    public void BuildResetSequenceSql_UsesRegclassCast()
    {
        var result = PostgreSqlBulkCopier.BuildResetSequenceSql(
            "public.seq1",
            "public.tbl1",
            "col1");

        // Must cast to regclass for setval
        Assert.Contains("::regclass", result);
    }

    [Fact]
    public void BuildResetSequenceSql_EmptyTable_IsCalledFalse()
    {
        var result = PostgreSqlBulkCopier.BuildResetSequenceSql(
            "public.users_id_seq",
            "public.users",
            "id");

        // When table is empty, MAX returns NULL, so IS NOT NULL → false, making is_called=false
        // This ensures next nextval() returns 1, not 2
        Assert.Contains("COALESCE((SELECT MAX(\"id\") FROM \"public\".\"users\") IS NOT NULL, false)", result);
    }

    [Fact]
    public void BuildResetSequenceSql_SeqNameWithSingleQuote_Escaped()
    {
        var result = PostgreSqlBulkCopier.BuildResetSequenceSql(
            "public.it's_seq",
            "public.tbl1",
            "col1");

        // Single quotes in sequence name must be escaped
        Assert.Contains("'public.it''s_seq'::regclass", result);
    }

    #endregion

    #region TopologicalSorter integration — FK dependency ordering

    [Fact]
    public void TopologicalSort_NoDependencies_PreservesOrder()
    {
        var tables = new[] { "public.a", "public.b", "public.c" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["public.a"] = [],
            ["public.b"] = [],
            ["public.c"] = []
        };

        var result = TopologicalSorter.Sort(tables.ToList(), deps);

        Assert.Equal(3, result.Count);
        Assert.Contains("public.a", result);
        Assert.Contains("public.b", result);
        Assert.Contains("public.c", result);
    }

    [Fact]
    public void TopologicalSort_LinearDependency_ParentBeforeChild()
    {
        // c → b → a
        var tables = new[] { "public.c", "public.b", "public.a" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["public.a"] = [],
            ["public.b"] = new(StringComparer.OrdinalIgnoreCase) { "public.a" },
            ["public.c"] = new(StringComparer.OrdinalIgnoreCase) { "public.b" }
        };

        var result = TopologicalSorter.Sort(tables.ToList(), deps);

        Assert.True(result.IndexOf("public.a") < result.IndexOf("public.b"));
        Assert.True(result.IndexOf("public.b") < result.IndexOf("public.c"));
    }

    [Fact]
    public void TopologicalSort_MultipleDependencies_AllParentsFirst()
    {
        // orders depends on users and products
        var tables = new[] { "public.orders", "public.users", "public.products" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["public.users"] = [],
            ["public.products"] = [],
            ["public.orders"] = new(StringComparer.OrdinalIgnoreCase) { "public.users", "public.products" }
        };

        var result = TopologicalSorter.Sort(tables.ToList(), deps);

        Assert.True(result.IndexOf("public.users") < result.IndexOf("public.orders"));
        Assert.True(result.IndexOf("public.products") < result.IndexOf("public.orders"));
    }

    [Fact]
    public void TopologicalSort_CircularDependency_AllTablesIncluded()
    {
        var tables = new[] { "public.a", "public.b", "public.c" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["public.a"] = new(StringComparer.OrdinalIgnoreCase) { "public.b" },
            ["public.b"] = new(StringComparer.OrdinalIgnoreCase) { "public.a" },
            ["public.c"] = []
        };

        var result = TopologicalSorter.Sort(tables.ToList(), deps);

        Assert.Equal(3, result.Count);
        Assert.Contains("public.a", result);
        Assert.Contains("public.b", result);
        Assert.Contains("public.c", result);
        // c has no deps — should be first
        Assert.Equal("public.c", result[0]);
    }

    [Fact]
    public void TopologicalSort_EmptyList_ReturnsEmpty()
    {
        var result = TopologicalSorter.Sort(
            new List<string>(),
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase));

        Assert.Empty(result);
    }

    [Fact]
    public void TopologicalSort_SingleTable_ReturnsSameTable()
    {
        var tables = new[] { "public.users" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["public.users"] = []
        };

        var result = TopologicalSorter.Sort(tables.ToList(), deps);

        Assert.Single(result);
        Assert.Equal("public.users", result[0]);
    }

    #endregion

    #region Timeout clamping

    [Theory]
    [InlineData(null, 600)]    // null → default (600)
    [InlineData(100, 100)]     // explicit in-range value
    [InlineData(10, 30)]       // below min → clamped to 30
    [InlineData(5000, 3600)]   // above max → clamped to 3600
    [InlineData(30, 30)]       // exact min
    [InlineData(3600, 3600)]   // exact max
    public void TimeoutClamping_ViaClampTimeout_ClampsCorrectly(int? value, int expected)
    {
        // PostgreSqlBulkCopier uses PgIdentifierHelper.ClampTimeout with DefaultTimeoutSeconds=600
        Assert.Equal(expected, PgIdentifierHelper.ClampTimeout(value, 600));
    }

    #endregion

    #region PgIdentifierHelper integration — quoting

    [Theory]
    [InlineData("public.users", "\"public\".\"users\"")]
    [InlineData("dbo.Users", "\"public\".\"Users\"")]
    [InlineData("[dbo].[Users]", "\"public\".\"Users\"")]
    [InlineData("sales.orders", "\"sales\".\"orders\"")]
    [InlineData("MyTable", "\"MyTable\"")]
    public void QuotePgName_VariousInputs_QuotesCorrectly(string input, string expected)
    {
        Assert.Equal(expected, PgIdentifierHelper.QuotePgName(input));
    }

    [Theory]
    [InlineData("id", "\"id\"")]
    [InlineData("UserName", "\"UserName\"")]
    [InlineData("has\"quote", "\"has\"\"quote\"")]
    [InlineData("has space", "\"has space\"")]
    [InlineData("123numeric", "\"123numeric\"")]
    public void QuoteIdentifier_VariousInputs_QuotesCorrectly(string input, string expected)
    {
        Assert.Equal(expected, PgIdentifierHelper.QuoteIdentifier(input));
    }

    #endregion

    #region Progress reporting — SynchronousProgress helper

    [Fact]
    public async Task CopyDataAsync_EmptyList_ProgressPhaseIsDataMigration()
    {
        var copier = new PostgreSqlBulkCopier();
        var reports = new List<MigrationProgress>();
        var progress = new SynchronousProgress<MigrationProgress>(reports.Add);

        await copier.CopyDataAsync(
            "Host=fake;Database=source",
            "Host=fake;Database=target",
            Array.Empty<string>(),
            progress);

        Assert.All(reports, r => Assert.Equal("DataMigration", r.Phase));
    }

    [Fact]
    public async Task CopyDataAsync_EmptyList_ProgressRowsProcessedIsZero()
    {
        var copier = new PostgreSqlBulkCopier();
        var reports = new List<MigrationProgress>();
        var progress = new SynchronousProgress<MigrationProgress>(reports.Add);

        await copier.CopyDataAsync(
            "Host=fake;Database=source",
            "Host=fake;Database=target",
            Array.Empty<string>(),
            progress);

        Assert.Single(reports);
        Assert.Equal(0, reports[0].RowsProcessed);
    }

    #endregion

    #region COPY command structure — text mode

    [Fact]
    public void BuildCopyExportCommand_UsesTextMode_NoFormatSpecifier()
    {
        var result = PostgreSqlBulkCopier.BuildCopyExportCommand(
            "public.users",
            new[] { "id" });

        // Text mode COPY does not include FORMAT specification
        Assert.DoesNotContain("FORMAT", result);
        Assert.DoesNotContain("BINARY", result);
        Assert.Contains("TO STDOUT", result);
    }

    [Fact]
    public void BuildCopyImportCommand_UsesTextMode_NoFormatSpecifier()
    {
        var result = PostgreSqlBulkCopier.BuildCopyImportCommand(
            "public.users",
            new[] { "id" });

        Assert.DoesNotContain("FORMAT", result);
        Assert.DoesNotContain("BINARY", result);
        Assert.Contains("FROM STDIN", result);
    }

    [Theory]
    [InlineData("public.users", new[] { "id", "name" },
        "COPY \"public\".\"users\" (\"id\", \"name\") TO STDOUT")]
    [InlineData("dbo.Products", new[] { "ProductId" },
        "COPY \"public\".\"Products\" (\"ProductId\") TO STDOUT")]
    [InlineData("custom.items", new[] { "a", "b", "c" },
        "COPY \"custom\".\"items\" (\"a\", \"b\", \"c\") TO STDOUT")]
    public void BuildCopyExportCommand_ParameterizedCases_GeneratesCorrectSql(
        string tableName, string[] columns, string expected)
    {
        Assert.Equal(expected, PostgreSqlBulkCopier.BuildCopyExportCommand(tableName, columns));
    }

    [Theory]
    [InlineData("public.users", new[] { "id", "name" },
        "COPY \"public\".\"users\" (\"id\", \"name\") FROM STDIN")]
    [InlineData("dbo.Products", new[] { "ProductId" },
        "COPY \"public\".\"Products\" (\"ProductId\") FROM STDIN")]
    public void BuildCopyImportCommand_ParameterizedCases_GeneratesCorrectSql(
        string tableName, string[] columns, string expected)
    {
        Assert.Equal(expected, PostgreSqlBulkCopier.BuildCopyImportCommand(tableName, columns));
    }

    #endregion

    /// <summary>
    /// Synchronous IProgress implementation for unit testing.
    /// Unlike Progress{T}, this invokes the callback synchronously on the calling thread.
    /// </summary>
    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public SynchronousProgress(Action<T> handler) => _handler = handler;
        public void Report(T value) => _handler(value);
    }
}