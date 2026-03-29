using System.Collections.Immutable;
using GroundControl.Host.Api.Generators.WebApiModule.Descriptors;

namespace GroundControl.Host.Api.Generators.Internals;

internal static class TopologicalSorter
{
    /// <summary>
    /// Sorts modules topologically using Kahn's algorithm with deterministic alphabetical tie-breaking.
    /// RunsBefore edges are converted to reverse RunsAfter edges before sorting.
    /// </summary>
    public static TopologicalSortResult Sort(ImmutableArray<ModuleDescriptor> modules)
    {
        var moduleMap = new Dictionary<string, ModuleDescriptor>();
        var adjacency = new Dictionary<string, List<string>>();
        var inDegree = new Dictionary<string, int>();

        foreach (var module in modules)
        {
            moduleMap[module.FullyQualifiedName] = module;
            adjacency[module.FullyQualifiedName] = new List<string>();
            inDegree[module.FullyQualifiedName] = 0;
        }

        // RunsAfter: if A has [RunsAfter<B>], B must come before A → edge B → A
        foreach (var module in modules)
        {
            foreach (var dep in module.RunsAfter)
            {
                if (adjacency.TryGetValue(dep.TargetFullyQualifiedName, out var value))
                {
                    value.Add(module.FullyQualifiedName);
                    inDegree[module.FullyQualifiedName]++;
                }
            }
        }

        // RunsBefore: if A has [RunsBefore<B>], A must come before B → edge A → B
        foreach (var module in modules)
        {
            foreach (var target in module.RunsBefore)
            {
                if (inDegree.TryGetValue(target, out var value))
                {
                    adjacency[module.FullyQualifiedName].Add(target);
                    inDegree[target] = ++value;
                }
            }
        }

        // Kahn's algorithm with alphabetical tie-breaking via SortedSet
        var queue = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var kvp in inDegree.Where(kvp => kvp.Value == 0))
        {
            queue.Add(kvp.Key);
        }

        var sorted = new List<ModuleDescriptor>();

        while (queue.Count > 0)
        {
            var current = queue.Min!;
            queue.Remove(current);
            sorted.Add(moduleMap[current]);

            foreach (var neighbor in adjacency[current])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                {
                    queue.Add(neighbor);
                }
            }
        }

        if (sorted.Count >= modules.Length)
        {
            return TopologicalSortResult.Sorted(sorted.ToImmutableArray());
        }

        var cycleParticipants = inDegree
            .Where(kvp => kvp.Value > 0)
            .Select(kvp => kvp.Key)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToImmutableArray();

        return TopologicalSortResult.Cycle(cycleParticipants);
    }
}