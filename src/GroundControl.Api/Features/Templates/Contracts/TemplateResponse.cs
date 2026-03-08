using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Templates.Contracts;

/// <summary>
/// Represents the API response body for a template.
/// </summary>
internal sealed record TemplateResponse
{
    /// <summary>
    /// Gets the unique identifier for the template.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the template name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the optional template description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the owning group identifier.
    /// </summary>
    public Guid? GroupId { get; init; }

    /// <summary>
    /// Gets the optimistic concurrency version.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// Gets the timestamp when the template was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the identifier of the user that created the template.
    /// </summary>
    public required Guid CreatedBy { get; init; }

    /// <summary>
    /// Gets the timestamp when the template was last updated.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Gets the identifier of the user that last updated the template.
    /// </summary>
    public required Guid UpdatedBy { get; init; }

    /// <summary>
    /// Creates a response contract from a persisted <see cref="Template" /> entity.
    /// </summary>
    /// <param name="template">The persisted template entity.</param>
    /// <returns>The API response contract.</returns>
    public static TemplateResponse From(Template template)
    {
        ArgumentNullException.ThrowIfNull(template);

        return new TemplateResponse
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            GroupId = template.GroupId,
            Version = template.Version,
            CreatedAt = template.CreatedAt,
            CreatedBy = template.CreatedBy,
            UpdatedAt = template.UpdatedAt,
            UpdatedBy = template.UpdatedBy,
        };
    }
}