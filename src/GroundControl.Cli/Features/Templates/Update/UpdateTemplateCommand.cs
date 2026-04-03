using System.CommandLine;

namespace GroundControl.Cli.Features.Templates.Update;

internal sealed class UpdateTemplateCommand : Command<UpdateTemplateHandler, UpdateTemplateOptions>
{
    public UpdateTemplateCommand()
        : base("update", "Update a template")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The template ID" };
        var nameOption = new Option<string?>("--name") { Description = "The new template name" };
        var descriptionOption = new Option<string?>("--description") { Description = "The new template description" };
        var groupIdOption = new Option<Guid?>("--group-id") { Description = "The new owning group ID" };
        var versionOption = new Option<long?>("--version") { Description = "The expected version for optimistic concurrency" };

        Arguments.Add(idArgument);
        Options.Add(nameOption);
        Options.Add(descriptionOption);
        Options.Add(groupIdOption);
        Options.Add(versionOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
            options.Name = parseResult.GetValue(nameOption);
            options.Description = parseResult.GetValue(descriptionOption);
            options.GroupId = parseResult.GetValue(groupIdOption);
            options.Version = parseResult.GetValue(versionOption);
        });
    }
}