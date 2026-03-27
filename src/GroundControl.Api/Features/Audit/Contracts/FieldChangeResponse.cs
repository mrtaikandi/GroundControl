using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Audit.Contracts;

internal sealed record FieldChangeResponse
{
    public required string Field { get; init; }

    public string? OldValue { get; init; }

    public string? NewValue { get; init; }

    public static FieldChangeResponse From(FieldChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        return new FieldChangeResponse
        {
            Field = change.Field,
            OldValue = change.OldValue,
            NewValue = change.NewValue,
        };
    }
}