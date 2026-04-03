using System.CommandLine;

namespace GroundControl.Cli.Features.Variables.Delete;

internal sealed class DeleteVariableCommand : Command<DeleteVariableHandler, DeleteVariableOptions>
{
    public DeleteVariableCommand()
        : base("delete", "Delete a variable")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The variable ID" };
        var versionOption = new Option<long?>("--version") { Description = "The expected version for optimistic concurrency" };
        var yesOption = new Option<bool>("--yes") { Description = "Skip confirmation prompt" };

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