using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Shared.Activity;

internal enum LiveActivityEventKind
{
    Activity,
    AuditRecord,
}

internal sealed record LiveActivityEvent
{
    public required LiveActivityEventKind Kind { get; init; }

    public LiveActivitySnapshot? Activity { get; init; }

    public AuditRecord? AuditRecord { get; init; }

    public static LiveActivityEvent FromActivity(LiveActivitySnapshot snapshot) => new()
    {
        Kind = LiveActivityEventKind.Activity,
        Activity = snapshot,
    };

    public static LiveActivityEvent FromAuditRecord(AuditRecord record) => new()
    {
        Kind = LiveActivityEventKind.AuditRecord,
        AuditRecord = record,
    };
}