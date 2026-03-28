using System.Collections.Immutable;

namespace GroundControl.Host.Api.Generators;

internal readonly record struct TopologicalSortResult(
    bool IsCycle,
    ImmutableArray<ModuleInfo> SortedModules,
    ImmutableArray<string> CycleParticipants)
{
    public static TopologicalSortResult Sorted(ImmutableArray<ModuleInfo> sortedModules) =>
        new(false, sortedModules, ImmutableArray<string>.Empty);

    public static TopologicalSortResult Cycle(ImmutableArray<string> cycleParticipants) =>
        new(true, ImmutableArray<ModuleInfo>.Empty, cycleParticipants);
}