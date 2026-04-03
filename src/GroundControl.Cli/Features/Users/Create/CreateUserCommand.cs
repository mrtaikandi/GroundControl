using System.CommandLine;

namespace GroundControl.Cli.Features.Users.Create;

internal sealed class CreateUserCommand : Command<CreateUserHandler, CreateUserOptions>
{
    public CreateUserCommand()
        : base("create", "Create a new user")
    {
        var usernameOption = new Option<string?>("--username", "The username");
        var emailOption = new Option<string?>("--email", "The email address");
        var passwordOption = new Option<string?>("--password", "The password (prompted securely in interactive mode)");
        var grantOption = new Option<string[]>("--grant", "Role ID to grant (can be specified multiple times)")
        {
            AllowMultipleArgumentsPerToken = true
        };

        Options.Add(usernameOption);
        Options.Add(emailOption);
        Options.Add(passwordOption);
        Options.Add(grantOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Username = parseResult.GetValue(usernameOption);
            options.Email = parseResult.GetValue(emailOption);
            options.Password = parseResult.GetValue(passwordOption);
            options.Grants = parseResult.GetValue(grantOption);
        });
    }
}