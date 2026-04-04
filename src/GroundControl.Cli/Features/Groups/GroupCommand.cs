using System.CommandLine;
using GroundControl.Cli.Features.Groups.Create;
using GroundControl.Cli.Features.Groups.Delete;
using GroundControl.Cli.Features.Groups.Get;
using GroundControl.Cli.Features.Groups.List;
using GroundControl.Cli.Features.Groups.Update;

namespace GroundControl.Cli.Features.Groups;

[RootCommand]
internal sealed class GroupCommand : Command
{
    public GroupCommand()
        : base("group", "Manage groups")
    {
        Subcommands.Add(new ListGroupsCommand());
        Subcommands.Add(new GetGroupCommand());
        Subcommands.Add(new CreateGroupCommand());
        Subcommands.Add(new UpdateGroupCommand());
        Subcommands.Add(new DeleteGroupCommand());
    }
}