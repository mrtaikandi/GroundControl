using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace GroundControl.Host.Api.Generators;

internal static class TopologicalSorter
{
    /// <summary>
    /// Sorts modules topologically using Kahn's algorithm with deterministic alphabetical tie-breaking.
    /// RunsBefore edges are converted to reverse RunsAfter edges before sorting.
    /// </summary>
    public static TopologicalSortResult Sort(ImmutableArray<ModuleInfo> modules)
    {
        var moduleMap = new Dictionary<string, ModuleInfo>();
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
                if (adjacency.ContainsKey(dep.TargetFullyQualifiedName))
                {
                    adjacency[dep.TargetFullyQualifiedName].Add(module.FullyQualifiedName);
                    inDegree[module.FullyQualifiedName]++;
                }
            }
        }

        // RunsBefore: if A has [RunsBefore<B>], A must come before B → edge A → B
        foreach (var module in modules)
        {
            foreach (var target in module.RunsBefore)
            {
                if (inDegree.ContainsKey(target))
                {
                    adjacency[module.FullyQualifiedName].Add(target);
                    inDegree[target]++;
                }
            }
        }

        // Kahn's algorithm with alphabetical tie-breaking via SortedSet
        var queue = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var kvp in inDegree)
        {
            if (kvp.Value == 0)
            {
                queue.Add(kvp.Key);
            }
        }

        var sorted = new List<ModuleInfo>();

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

        if (sorted.Count < modules.Length)
        {
            var cycleParticipants = inDegree
                .Where(kvp => kvp.Value > 0)
                .Select(kvp => kvp.Key)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToImmutableArray();

            return TopologicalSortResult.Cycle(cycleParticipants);
        }

        return TopologicalSortResult.Sorted(sorted.ToImmutableArray());
    }
}