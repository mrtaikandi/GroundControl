using System.CommandLine;

namespace GroundControl.Cli.Features.Templates.Create;

internal sealed class CreateTemplateCommand : Command<CreateTemplateHandler, CreateTemplateOptions>
{
    public CreateTemplateCommand()
        : base("create", "Create a new template")
    {
        var nameOption = new Option<string?>("--name") { Description = "The template name (Required)" };
        var descriptionOption = new Option<string?>("--description") { Description = "The template description" };
        var groupIdOption = new Option<Guid?>("--group-id") { Description = "The owning group ID" };

        Options.Add(nameOption);
        Options.Add(descriptionOption);
        Options.Add(groupIdOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Name = parseResult.GetValue(nameOption);
            options.Description = parseResult.GetValue(descriptionOption);
            options.GroupId = parseResult.GetValue(groupIdOption);
        });
    }
}