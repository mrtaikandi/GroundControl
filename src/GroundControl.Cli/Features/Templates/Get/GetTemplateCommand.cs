using System.CommandLine;

namespace GroundControl.Cli.Features.Templates.Get;

internal sealed class GetTemplateCommand : Command<GetTemplateHandler, GetTemplateOptions>
{
    public GetTemplateCommand()
        : base("get", "Get a template by ID")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The template ID" };

        Arguments.Add(idArgument);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
        });
    }
}