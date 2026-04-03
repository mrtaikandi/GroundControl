using System.CommandLine;

namespace GroundControl.Cli.Features.Groups.Update;

internal sealed class UpdateGroupCommand : Command<UpdateGroupHandler, UpdateGroupOptions>
{
    public UpdateGroupCommand()
        : base("update", "Update a group")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The group ID" };
        var nameOption = new Option<string?>("--name") { Description = "The new group name" };
        var descriptionOption = new Option<string?>("--description") { Description = "The new group description" };
        var versionOption = new Option<long?>("--version") { Description = "The expected version for optimistic concurrency" };

        Arguments.Add(idArgument);
        Options.Add(nameOption);
        Options.Add(descriptionOption);
        Options.Add(versionOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
            options.Name = parseResult.GetValue(nameOption);
            options.Description = parseResult.GetValue(descriptionOption);
            options.Version = parseResult.GetValue(versionOption);
        });
    }
}