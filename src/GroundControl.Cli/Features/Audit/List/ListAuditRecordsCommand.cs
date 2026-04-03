using System.CommandLine;

namespace GroundControl.Cli.Features.Audit.List;

internal sealed class ListAuditRecordsCommand : Command<ListAuditRecordsHandler, ListAuditRecordsOptions>
{
    public ListAuditRecordsCommand()
        : base("list", "List audit records")
    {
        var entityTypeOption = new Option<string?>("--entity-type", "Filter by entity type");
        var entityIdOption = new Option<Guid?>("--entity-id", "Filter by entity ID");

        Options.Add(entityTypeOption);
        Options.Add(entityIdOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.EntityType = parseResult.GetValue(entityTypeOption);
            options.EntityId = parseResult.GetValue(entityIdOption);
        });
    }
}