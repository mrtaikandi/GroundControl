using System.CommandLine;

namespace GroundControl.Cli.Features.Roles.Create;

internal sealed class CreateRoleCommand : Command<CreateRoleHandler, CreateRoleOptions>
{
    public CreateRoleCommand()
        : base("create", "Create a new role")
    {
        var nameOption = new Option<string?>("--name") { Description = "The role name" };
        var permissionsOption = new Option<string?>("--permissions") { Description = "Comma-separated permission strings" };
        var descriptionOption = new Option<string?>("--description") { Description = "The role description" };

        Options.Add(nameOption);
        Options.Add(permissionsOption);
        Options.Add(descriptionOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Name = parseResult.GetValue(nameOption);
            options.Permissions = parseResult.GetValue(permissionsOption);
            options.Description = parseResult.GetValue(descriptionOption);
        });
    }
}