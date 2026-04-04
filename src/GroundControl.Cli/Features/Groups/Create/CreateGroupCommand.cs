using System.CommandLine;

namespace GroundControl.Cli.Features.Groups.Create;

internal sealed class CreateGroupCommand : Command<CreateGroupHandler, CreateGroupOptions>
{
    public CreateGroupCommand()
        : base("create", "Create a new group")
    {
        var nameOption = new Option<string?>("--name") { Description = "The group name (Required)" };
        var descriptionOption = new Option<string?>("--description") { Description = "The group description" };

        Options.Add(nameOption);
        Options.Add(descriptionOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Name = parseResult.GetValue(nameOption);
            options.Description = parseResult.GetValue(descriptionOption);
        });
    }
}