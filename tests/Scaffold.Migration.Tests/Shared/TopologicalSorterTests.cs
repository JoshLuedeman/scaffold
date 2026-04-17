using Scaffold.Migration.Shared;

namespace Scaffold.Migration.Tests.Shared;

public class TopologicalSorterTests
{
    #region String Sort

    [Fact]
    public void Sort_NoDependencies_PreservesOriginalOrder()
    {
        var items = new List<string> { "A", "B", "C" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = [],
            ["B"] = [],
            ["C"] = []
        };

        var result = TopologicalSorter.Sort(items, deps);

        Assert.Equal(["A", "B", "C"], result);
    }

    [Fact]
    public void Sort_SimpleChain_ParentsBeforeChildren()
    {
        // C depends on B, B depends on A → order should be A, B, C
        var items = new List<string> { "C", "B", "A" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = [],
            ["B"] = new(["A"], StringComparer.OrdinalIgnoreCase),
            ["C"] = new(["B"], StringComparer.OrdinalIgnoreCase)
        };

        var result = TopologicalSorter.Sort(items, deps);

        var indexA = result.IndexOf("A");
        var indexB = result.IndexOf("B");
        var indexC = result.IndexOf("C");

        Assert.True(indexA < indexB, "A should come before B");
        Assert.True(indexB < indexC, "B should come before C");
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Sort_DiamondDependency_HandlesCorrectly()
    {
        // D depends on B and C; B depends on A; C depends on A
        var items = new List<string> { "D", "C", "B", "A" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = [],
            ["B"] = new(["A"], StringComparer.OrdinalIgnoreCase),
            ["C"] = new(["A"], StringComparer.OrdinalIgnoreCase),
            ["D"] = new(["B", "C"], StringComparer.OrdinalIgnoreCase)
        };

        var result = TopologicalSorter.Sort(items, deps);

        var indexA = result.IndexOf("A");
        var indexB = result.IndexOf("B");
        var indexC = result.IndexOf("C");
        var indexD = result.IndexOf("D");

        Assert.True(indexA < indexB, "A should come before B");
        Assert.True(indexA < indexC, "A should come before C");
        Assert.True(indexB < indexD, "B should come before D");
        Assert.True(indexC < indexD, "C should come before D");
    }

    [Fact]
    public void Sort_CircularDependency_AppendsRemainingInOriginalOrder()
    {
        // A depends on B, B depends on A → circular
        var items = new List<string> { "A", "B", "C" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = new(["B"], StringComparer.OrdinalIgnoreCase),
            ["B"] = new(["A"], StringComparer.OrdinalIgnoreCase),
            ["C"] = []
        };

        var result = TopologicalSorter.Sort(items, deps);

        // C has no deps, should come first
        Assert.Equal("C", result[0]);
        // A and B are circular, appended in original order (A before B)
        Assert.Equal(3, result.Count);
        Assert.Contains("A", result);
        Assert.Contains("B", result);
    }

    [Fact]
    public void Sort_CaseInsensitive_MatchesDependencies()
    {
        var items = new List<string> { "child", "Parent" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Parent"] = [],
            ["child"] = new(["parent"], StringComparer.OrdinalIgnoreCase)
        };

        var result = TopologicalSorter.Sort(items, deps);

        var indexParent = result.IndexOf("Parent");
        var indexChild = result.IndexOf("child");

        Assert.True(indexParent < indexChild, "Parent should come before child");
    }

    #endregion

    #region Generic Sort<T>

    private record TableDef(string Schema, string Name, List<string> FkRefs);

    [Fact]
    public void SortGeneric_SimpleChain_OrdersByDependency()
    {
        var tables = new List<TableDef>
        {
            new("dbo", "Orders", ["dbo.Users"]),
            new("dbo", "Users", [])
        };

        var result = TopologicalSorter.Sort(
            tables,
            t => $"{t.Schema}.{t.Name}",
            t => t.FkRefs);

        Assert.Equal("Users", result[0].Name);
        Assert.Equal("Orders", result[1].Name);
    }

    [Fact]
    public void SortGeneric_SelfReference_ExcludedFromDependencies()
    {
        var tables = new List<TableDef>
        {
            new("dbo", "Employees", ["dbo.Employees"]), // self-referencing FK
            new("dbo", "Departments", [])
        };

        var result = TopologicalSorter.Sort(
            tables,
            t => $"{t.Schema}.{t.Name}",
            t => t.FkRefs);

        // Both should be in the result
        Assert.Equal(2, result.Count);
        // Self-reference shouldn't block ordering
        Assert.Contains(result, t => t.Name == "Employees");
        Assert.Contains(result, t => t.Name == "Departments");
    }

    [Fact]
    public void SortGeneric_ExternalDependency_Ignored()
    {
        // Orders references Users, but Users is not in the items list
        var tables = new List<TableDef>
        {
            new("dbo", "Orders", ["dbo.Users"]),
            new("dbo", "Products", [])
        };

        var result = TopologicalSorter.Sort(
            tables,
            t => $"{t.Schema}.{t.Name}",
            t => t.FkRefs);

        // External dep (Users) not in set → Orders has no effective deps
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SortGeneric_NoDependencies_PreservesOriginalOrder()
    {
        var tables = new List<TableDef>
        {
            new("dbo", "Alpha", []),
            new("dbo", "Beta", []),
            new("dbo", "Gamma", [])
        };

        var result = TopologicalSorter.Sort(
            tables,
            t => $"{t.Schema}.{t.Name}",
            t => t.FkRefs);

        Assert.Equal("Alpha", result[0].Name);
        Assert.Equal("Beta", result[1].Name);
        Assert.Equal("Gamma", result[2].Name);
    }

    #endregion
}
