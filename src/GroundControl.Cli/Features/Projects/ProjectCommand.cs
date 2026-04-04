using System.CommandLine;
using GroundControl.Cli.Features.Projects.Create;
using GroundControl.Cli.Features.Projects.Delete;
using GroundControl.Cli.Features.Projects.Get;
using GroundControl.Cli.Features.Projects.List;
using GroundControl.Cli.Features.Projects.Update;

namespace GroundControl.Cli.Features.Projects;

[RootCommand]
internal sealed class ProjectCommand : Command
{
    public ProjectCommand()
        : base("project", "Manage projects")
    {
        Subcommands.Add(new ListProjectsCommand());
        Subcommands.Add(new GetProjectCommand());
        Subcommands.Add(new CreateProjectCommand());
        Subcommands.Add(new UpdateProjectCommand());
        Subcommands.Add(new DeleteProjectCommand());
    }
}