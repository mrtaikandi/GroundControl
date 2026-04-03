namespace GroundControl.Cli.Features.Audit.List;

internal sealed class ListAuditRecordsOptions
{
    public string? EntityType { get; set; }

    public Guid? EntityId { get; set; }
}