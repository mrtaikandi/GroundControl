using System.CommandLine;

namespace GroundControl.Cli.Features.Projects.Update;

internal sealed class UpdateProjectCommand : Command<UpdateProjectHandler, UpdateProjectOptions>
{
    public UpdateProjectCommand()
        : base("update", "Update a project")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The project ID" };
        var nameOption = new Option<string?>("--name") { Description = "The new project name" };
        var descriptionOption = new Option<string?>("--description") { Description = "The new description" };
        var groupIdOption = new Option<Guid?>("--group-id") { Description = "The new owning group ID" };
        var templateIdsOption = new Option<string?>("--template-ids") { Description = "Comma-separated template IDs (replaces existing)" };
        var versionOption = new Option<long?>("--version") { Description = "The expected version for optimistic concurrency" };

        Arguments.Add(idArgument);
        Options.Add(nameOption);
        Options.Add(descriptionOption);
        Options.Add(groupIdOption);
        Options.Add(templateIdsOption);
        Options.Add(versionOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
            options.Name = parseResult.GetValue(nameOption);
            options.Description = parseResult.GetValue(descriptionOption);
            options.GroupId = parseResult.GetValue(groupIdOption);
            options.TemplateIds = parseResult.GetValue(templateIdsOption);
            options.Version = parseResult.GetValue(versionOption);
        });
    }
}