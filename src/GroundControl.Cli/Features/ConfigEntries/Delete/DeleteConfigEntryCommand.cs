using System.CommandLine;

namespace GroundControl.Cli.Features.ConfigEntries.Delete;

internal sealed class DeleteConfigEntryCommand : Command<DeleteConfigEntryHandler, DeleteConfigEntryOptions>
{
    public DeleteConfigEntryCommand()
        : base("delete", "Delete a configuration entry")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The configuration entry ID" };
        var versionOption = new Option<long?>("--version", "The expected version for optimistic concurrency");
        var yesOption = new Option<bool>("--yes", "Skip confirmation prompt");

        Arguments.Add(idArgument);
        Options.Add(versionOption);
        Options.Add(yesOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
            options.Version = parseResult.GetValue(versionOption);
            options.Yes = parseResult.GetValue(yesOption);
        });
    }
}