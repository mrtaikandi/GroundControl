using System.CommandLine;
using GroundControl.Cli.Features.Users.Create;
using GroundControl.Cli.Features.Users.Delete;
using GroundControl.Cli.Features.Users.Get;
using GroundControl.Cli.Features.Users.List;
using GroundControl.Cli.Features.Users.Update;

namespace GroundControl.Cli.Features.Users;

[RootCommand]
internal sealed class UserCommand : Command
{
    public UserCommand()
        : base("user", "Manage users")
    {
        Subcommands.Add(new ListUsersCommand());
        Subcommands.Add(new GetUserCommand());
        Subcommands.Add(new CreateUserCommand());
        Subcommands.Add(new UpdateUserCommand());
        Subcommands.Add(new DeleteUserCommand());
    }
}