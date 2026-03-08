using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Features.Templates.Contracts;

/// <summary>
/// Represents the request body for creating a template.
/// </summary>
internal sealed record CreateTemplateRequest
{
    /// <summary>
    /// Gets the template name.
    /// </summary>
    /// <remarks>Maximum length: 200 characters.</remarks>
    [Required]
    [MaxLength(200)]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the optional template description.
    /// </summary>
    /// <remarks>Maximum length: 500 characters.</remarks>
    [MaxLength(500)]
    public string? Description { get; init; }

    /// <summary>
    /// Gets the optional owning group identifier.
    /// </summary>
    public Guid? GroupId { get; init; }
}