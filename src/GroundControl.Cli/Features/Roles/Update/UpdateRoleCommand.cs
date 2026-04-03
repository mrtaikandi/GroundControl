using System.CommandLine;

namespace GroundControl.Cli.Features.Roles.Update;

internal sealed class UpdateRoleCommand : Command<UpdateRoleHandler, UpdateRoleOptions>
{
    public UpdateRoleCommand()
        : base("update", "Update a role")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The role ID" };
        var nameOption = new Option<string?>("--name", "The new role name");
        var permissionsOption = new Option<string?>("--permissions", "Comma-separated permission strings");
        var descriptionOption = new Option<string?>("--description", "The new description");
        var versionOption = new Option<long?>("--version", "The expected version for optimistic concurrency");

        Arguments.Add(idArgument);
        Options.Add(nameOption);
        Options.Add(permissionsOption);
        Options.Add(descriptionOption);
        Options.Add(versionOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
            options.Name = parseResult.GetValue(nameOption);
            options.Permissions = parseResult.GetValue(permissionsOption);
            options.Description = parseResult.GetValue(descriptionOption);
            options.Version = parseResult.GetValue(versionOption);
        });
    }
}