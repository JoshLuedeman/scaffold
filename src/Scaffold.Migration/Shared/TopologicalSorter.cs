namespace Scaffold.Migration.Shared;

/// <summary>
/// Generic topological sort using Kahn's algorithm.
/// Used for FK dependency ordering across all migration engines.
/// </summary>
public static class TopologicalSorter
{
    /// <summary>
    /// Sorts string table names by dependencies. Parents before children.
    /// If a cycle is detected, remaining items are appended at the end.
    /// </summary>
    public static List<string> Sort(
        IReadOnlyList<string> items,
        Dictionary<string, HashSet<string>> dependsOn)
    {
        var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in items) inDegree[t] = dependsOn[t].Count;

        var queue = new Queue<string>(items.Where(t => inDegree[t] == 0));
        var ordered = new List<string>(items.Count);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            ordered.Add(current);

            foreach (var (child, parents) in dependsOn)
            {
                if (parents.Remove(current))
                {
                    inDegree[child]--;
                    if (inDegree[child] == 0)
                        queue.Enqueue(child);
                }
            }
        }

        // Append remaining (circular dependencies) in original order
        foreach (var t in items)
        {
            if (!ordered.Contains(t))
                ordered.Add(t);
        }

        return ordered;
    }

    /// <summary>
    /// Sorts objects by dependencies using a key selector.
    /// </summary>
    public static List<T> Sort<T>(
        IReadOnlyList<T> items,
        Func<T, string> keySelector,
        Func<T, IEnumerable<string>> dependencySelector)
    {
        var itemKeys = new HashSet<string>(items.Select(keySelector), StringComparer.OrdinalIgnoreCase);
        var itemByKey = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var key = keySelector(item);
            itemByKey[key] = item;
            deps[key] = new HashSet<string>(
                dependencySelector(item).Where(d => itemKeys.Contains(d) && !d.Equals(key, StringComparison.OrdinalIgnoreCase)),
                StringComparer.OrdinalIgnoreCase);
        }

        var sortedKeys = Sort(items.Select(keySelector).ToList(), deps);
        return sortedKeys.Select(k => itemByKey[k]).ToList();
    }
}
