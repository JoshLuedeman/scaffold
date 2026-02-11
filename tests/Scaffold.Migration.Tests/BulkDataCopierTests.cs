using Scaffold.Migration.SqlServer;

namespace Scaffold.Migration.Tests;

public class BulkDataCopierTests
{
    #region TopologicalSort — parents before children

    [Fact]
    public void TopologicalSort_ParentBeforeChild()
    {
        var tables = new List<string> { "dbo.Orders", "dbo.Customers" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["dbo.Orders"] = new(StringComparer.OrdinalIgnoreCase) { "dbo.Customers" },
            ["dbo.Customers"] = new(StringComparer.OrdinalIgnoreCase)
        };

        var result = BulkDataCopier.TopologicalSort(tables, deps);

        Assert.Equal(2, result.Count);
        Assert.True(result.IndexOf("dbo.Customers") < result.IndexOf("dbo.Orders"),
            "Parent table (Customers) must appear before child table (Orders).");
    }

    [Fact]
    public void TopologicalSort_MultiLevel_Grandparent_Parent_Child()
    {
        var tables = new List<string> { "dbo.LineItems", "dbo.Orders", "dbo.Customers" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["dbo.Customers"] = new(StringComparer.OrdinalIgnoreCase),
            ["dbo.Orders"] = new(StringComparer.OrdinalIgnoreCase) { "dbo.Customers" },
            ["dbo.LineItems"] = new(StringComparer.OrdinalIgnoreCase) { "dbo.Orders" }
        };

        var result = BulkDataCopier.TopologicalSort(tables, deps);

        Assert.True(result.IndexOf("dbo.Customers") < result.IndexOf("dbo.Orders"));
        Assert.True(result.IndexOf("dbo.Orders") < result.IndexOf("dbo.LineItems"));
    }

    [Fact]
    public void TopologicalSort_ChildWithMultipleParents()
    {
        var tables = new List<string> { "dbo.OrderItems", "dbo.Orders", "dbo.Products" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["dbo.Orders"] = new(StringComparer.OrdinalIgnoreCase),
            ["dbo.Products"] = new(StringComparer.OrdinalIgnoreCase),
            ["dbo.OrderItems"] = new(StringComparer.OrdinalIgnoreCase) { "dbo.Orders", "dbo.Products" }
        };

        var result = BulkDataCopier.TopologicalSort(tables, deps);

        Assert.True(result.IndexOf("dbo.Orders") < result.IndexOf("dbo.OrderItems"));
        Assert.True(result.IndexOf("dbo.Products") < result.IndexOf("dbo.OrderItems"));
    }

    #endregion

    #region TopologicalSort — no dependencies

    [Fact]
    public void TopologicalSort_NoDependencies_ReturnsAllTables()
    {
        var tables = new List<string> { "dbo.A", "dbo.B", "dbo.C" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["dbo.A"] = new(StringComparer.OrdinalIgnoreCase),
            ["dbo.B"] = new(StringComparer.OrdinalIgnoreCase),
            ["dbo.C"] = new(StringComparer.OrdinalIgnoreCase)
        };

        var result = BulkDataCopier.TopologicalSort(tables, deps);

        Assert.Equal(3, result.Count);
        Assert.Contains("dbo.A", result);
        Assert.Contains("dbo.B", result);
        Assert.Contains("dbo.C", result);
    }

    [Fact]
    public void TopologicalSort_SingleTable_ReturnsSingleTable()
    {
        var tables = new List<string> { "dbo.Users" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["dbo.Users"] = new(StringComparer.OrdinalIgnoreCase)
        };

        var result = BulkDataCopier.TopologicalSort(tables, deps);

        Assert.Single(result);
        Assert.Equal("dbo.Users", result[0]);
    }

    #endregion

    #region TopologicalSort — circular dependencies

    [Fact]
    public void TopologicalSort_CircularDependency_DoesNotInfiniteLoop_ReturnsAllTables()
    {
        var tables = new List<string> { "dbo.A", "dbo.B" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["dbo.A"] = new(StringComparer.OrdinalIgnoreCase) { "dbo.B" },
            ["dbo.B"] = new(StringComparer.OrdinalIgnoreCase) { "dbo.A" }
        };

        // Must complete without hanging and include all tables
        var result = BulkDataCopier.TopologicalSort(tables, deps);

        Assert.Equal(2, result.Count);
        Assert.Contains("dbo.A", result);
        Assert.Contains("dbo.B", result);
    }

    [Fact]
    public void TopologicalSort_ThreeWayCycle_ReturnsAllTables()
    {
        var tables = new List<string> { "dbo.A", "dbo.B", "dbo.C" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["dbo.A"] = new(StringComparer.OrdinalIgnoreCase) { "dbo.C" },
            ["dbo.B"] = new(StringComparer.OrdinalIgnoreCase) { "dbo.A" },
            ["dbo.C"] = new(StringComparer.OrdinalIgnoreCase) { "dbo.B" }
        };

        var result = BulkDataCopier.TopologicalSort(tables, deps);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void TopologicalSort_PartialCycleWithIndependentTable()
    {
        // dbo.X has no deps, dbo.A <-> dbo.B form a cycle
        var tables = new List<string> { "dbo.X", "dbo.A", "dbo.B" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["dbo.X"] = new(StringComparer.OrdinalIgnoreCase),
            ["dbo.A"] = new(StringComparer.OrdinalIgnoreCase) { "dbo.B" },
            ["dbo.B"] = new(StringComparer.OrdinalIgnoreCase) { "dbo.A" }
        };

        var result = BulkDataCopier.TopologicalSort(tables, deps);

        Assert.Equal(3, result.Count);
        // The independent table should be first
        Assert.Equal("dbo.X", result[0]);
    }

    #endregion

    #region QuoteName

    [Theory]
    [InlineData("dbo.Users", "[dbo].[Users]")]
    [InlineData("schema1.TableA", "[schema1].[TableA]")]
    [InlineData("SinglePart", "[SinglePart]")]
    [InlineData("[dbo].[Users]", "[dbo].[Users]")]
    public void QuoteName_FormatsCorrectly(string input, string expected)
    {
        var result = BulkDataCopier.QuoteName(input);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Constraint disable/enable order

    [Fact]
    public void TopologicalSort_ConstraintOrder_DisableForwardEnableReverse()
    {
        // This test verifies the ordering contract that ToggleConstraintsAsync relies on:
        // disable iterates forward (parents first), enable iterates in reverse (children first).
        var tables = new List<string> { "dbo.Child", "dbo.Parent" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["dbo.Parent"] = new(StringComparer.OrdinalIgnoreCase),
            ["dbo.Child"] = new(StringComparer.OrdinalIgnoreCase) { "dbo.Parent" }
        };

        var ordered = BulkDataCopier.TopologicalSort(tables, deps);

        // Forward order: Parent first
        Assert.Equal("dbo.Parent", ordered[0]);
        Assert.Equal("dbo.Child", ordered[1]);

        // Reverse order (used for re-enabling): Child first
        var reversed = ordered.AsEnumerable().Reverse().ToList();
        Assert.Equal("dbo.Child", reversed[0]);
        Assert.Equal("dbo.Parent", reversed[1]);
    }

    #endregion
}
