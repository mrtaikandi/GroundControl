using System.CommandLine;

namespace GroundControl.Cli.Features.Projects.Create;

internal sealed class CreateProjectCommand : Command<CreateProjectHandler, CreateProjectOptions>
{
    public CreateProjectCommand()
        : base("create", "Create a new project")
    {
        var nameOption = new Option<string?>("--name") { Description = "The project name" };
        var descriptionOption = new Option<string?>("--description") { Description = "The project description" };
        var groupIdOption = new Option<Guid?>("--group-id") { Description = "The owning group ID" };
        var templateIdsOption = new Option<string?>("--template-ids") { Description = "Comma-separated template IDs" };

        Options.Add(nameOption);
        Options.Add(descriptionOption);
        Options.Add(groupIdOption);
        Options.Add(templateIdsOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Name = parseResult.GetValue(nameOption);
            options.Description = parseResult.GetValue(descriptionOption);
            options.GroupId = parseResult.GetValue(groupIdOption);
            options.TemplateIds = parseResult.GetValue(templateIdsOption);
        });
    }
}