using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Variables.Contracts;

internal sealed record VariableResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required VariableScope Scope { get; init; }

    public Guid? GroupId { get; init; }

    public Guid? ProjectId { get; init; }

    public required IReadOnlyCollection<ScopedValue> Values { get; init; }

    public required bool IsSensitive { get; init; }

    public required long Version { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required Guid CreatedBy { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public required Guid UpdatedBy { get; init; }

    public static VariableResponse From(Variable variable)
    {
        ArgumentNullException.ThrowIfNull(variable);

        return new VariableResponse
        {
            Id = variable.Id,
            Name = variable.Name,
            Description = variable.Description,
            Scope = variable.Scope,
            GroupId = variable.GroupId,
            ProjectId = variable.ProjectId,
            Values = [.. variable.Values],
            IsSensitive = variable.IsSensitive,
            Version = variable.Version,
            CreatedAt = variable.CreatedAt,
            CreatedBy = variable.CreatedBy,
            UpdatedAt = variable.UpdatedAt,
            UpdatedBy = variable.UpdatedBy,
        };
    }
}