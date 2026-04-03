using System.CommandLine;

namespace GroundControl.Cli.Features.Users.Update;

internal sealed class UpdateUserCommand : Command<UpdateUserHandler, UpdateUserOptions>
{
    public UpdateUserCommand()
        : base("update", "Update a user")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The user ID" };
        var usernameOption = new Option<string?>("--username") { Description = "The new username" };
        var emailOption = new Option<string?>("--email") { Description = "The new email address" };
        var isActiveOption = new Option<bool?>("--active") { Description = "Whether the user is active" };
        var grantOption = new Option<string[]>("--grant")
        {
            Description = "Role ID to grant (can be specified multiple times)",
            AllowMultipleArgumentsPerToken = true
        };
        var versionOption = new Option<long?>("--version") { Description = "The expected version for optimistic concurrency" };

        Arguments.Add(idArgument);
        Options.Add(usernameOption);
        Options.Add(emailOption);
        Options.Add(isActiveOption);
        Options.Add(grantOption);
        Options.Add(versionOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
            options.Username = parseResult.GetValue(usernameOption);
            options.Email = parseResult.GetValue(emailOption);
            options.IsActive = parseResult.GetValue(isActiveOption);
            options.Grants = parseResult.GetValue(grantOption);
            options.Version = parseResult.GetValue(versionOption);
        });
    }
}