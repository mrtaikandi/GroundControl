using System.CommandLine;

namespace GroundControl.Cli.Features.Users.Delete;

internal sealed class DeleteUserCommand : Command<DeleteUserHandler, DeleteUserOptions>
{
    public DeleteUserCommand()
        : base("delete", "Delete a user")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The user ID" };
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