using System.CommandLine;
using GroundControl.Cli.Features.Auth.Login;
using GroundControl.Cli.Features.Auth.Logout;
using GroundControl.Cli.Features.Auth.Status;

namespace GroundControl.Cli.Features.Auth;

[RootCommand]
internal sealed class AuthCommand : Command
{
    public AuthCommand()
        : base("auth", "Manage server credentials")
    {
        Subcommands.Add(new LoginCommand());
        Subcommands.Add(new LogoutCommand());
        Subcommands.Add(new StatusCommand());
    }
}