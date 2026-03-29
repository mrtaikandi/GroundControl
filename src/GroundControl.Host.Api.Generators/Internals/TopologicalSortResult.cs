using System.Collections.Immutable;
using GroundControl.Host.Api.Generators.WebApiModule.Descriptors;

namespace GroundControl.Host.Api.Generators.Internals;

internal readonly record struct TopologicalSortResult(
    bool IsCycle,
    ImmutableArray<ModuleDescriptor> SortedModules,
    ImmutableArray<string> CycleParticipants)
{
    public static TopologicalSortResult Sorted(ImmutableArray<ModuleDescriptor> sortedModules) =>
        new(false, sortedModules, []);

    public static TopologicalSortResult Cycle(ImmutableArray<string> cycleParticipants) =>
        new(true, [], cycleParticipants);
}