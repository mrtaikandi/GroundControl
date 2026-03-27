using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Audit.Contracts;

internal sealed record AuditRecordResponse
{
    public required Guid Id { get; init; }

    public required string EntityType { get; init; }

    public required Guid EntityId { get; init; }

    public Guid? GroupId { get; init; }

    public required string Action { get; init; }

    public required Guid PerformedBy { get; init; }

    public required IReadOnlyList<FieldChangeResponse> Changes { get; init; }

    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    public required DateTimeOffset PerformedAt { get; init; }

    public static AuditRecordResponse From(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new AuditRecordResponse
        {
            Id = record.Id,
            EntityType = record.EntityType,
            EntityId = record.EntityId,
            GroupId = record.GroupId,
            Action = record.Action,
            PerformedBy = record.PerformedBy,
            Changes = record.Changes.Select(FieldChangeResponse.From).ToList(),
            Metadata = record.Metadata.Count > 0 ? record.Metadata : null,
            PerformedAt = record.PerformedAt,
        };
    }
}