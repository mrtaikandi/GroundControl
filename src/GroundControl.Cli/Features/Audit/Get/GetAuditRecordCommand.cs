using System.CommandLine;

namespace GroundControl.Cli.Features.Audit.Get;

internal sealed class GetAuditRecordCommand : Command<GetAuditRecordHandler, GetAuditRecordOptions>
{
    public GetAuditRecordCommand()
        : base("get", "Get an audit record by ID")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The audit record ID" };

        Arguments.Add(idArgument);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
        });
    }
}