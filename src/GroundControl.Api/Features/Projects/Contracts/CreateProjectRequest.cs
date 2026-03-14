using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Features.Projects.Contracts;

/// <summary>
/// Represents the request body for creating a project.
/// </summary>
internal sealed record CreateProjectRequest
{
    /// <summary>
    /// Gets the project name.
    /// </summary>
    /// <remarks>Maximum length: 200 characters.</remarks>
    [Required]
    [MaxLength(200)]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the optional project description.
    /// </summary>
    /// <remarks>Maximum length: 500 characters.</remarks>
    [MaxLength(500)]
    public string? Description { get; init; }

    /// <summary>
    /// Gets the optional owning group identifier.
    /// </summary>
    public Guid? GroupId { get; init; }

    /// <summary>
    /// Gets the optional ordered template identifiers to apply to the project.
    /// </summary>
    public List<Guid>? TemplateIds { get; init; }
}