using System.CommandLine;
using GroundControl.Cli.Features.ClientConfig.Get;

namespace GroundControl.Cli.Features.ClientConfig;

[RootCommand]
internal sealed class ClientConfigCommand : Command
{
    public ClientConfigCommand()
        : base("client-config", "Test client configuration resolution")
    {
        Subcommands.Add(new GetClientConfigCommand());
    }
}