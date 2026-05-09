using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Features.Projects.Contracts;

internal sealed class GroupedProjectsQuery
{
    public const int DefaultPerGroup = 10;

    public string? Search { get; init; }

    [Range(1, 100)]
    public int? PerGroup { get; init; }
}