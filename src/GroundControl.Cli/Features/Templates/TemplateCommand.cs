using System.CommandLine;
using GroundControl.Cli.Features.Templates.Create;
using GroundControl.Cli.Features.Templates.Delete;
using GroundControl.Cli.Features.Templates.Get;
using GroundControl.Cli.Features.Templates.List;
using GroundControl.Cli.Features.Templates.Update;

namespace GroundControl.Cli.Features.Templates;

[RootCommand<TemplateDependencyModule>]
internal sealed class TemplateCommand : Command
{
    public TemplateCommand()
        : base("template", "Manage templates")
    {
        Subcommands.Add(new ListTemplatesCommand());
        Subcommands.Add(new GetTemplateCommand());
        Subcommands.Add(new CreateTemplateCommand());
        Subcommands.Add(new UpdateTemplateCommand());
        Subcommands.Add(new DeleteTemplateCommand());
    }
}