using System.CommandLine;
using GroundControl.Cli.Features.Clients.Create;
using GroundControl.Cli.Features.Clients.Delete;
using GroundControl.Cli.Features.Clients.Get;
using GroundControl.Cli.Features.Clients.List;
using GroundControl.Cli.Features.Clients.Update;

namespace GroundControl.Cli.Features.Clients;

[RootCommand]
internal sealed class ClientCommand : Command
{
    public ClientCommand()
        : base("client", "Manage clients")
    {
        Subcommands.Add(new ListClientsCommand());
        Subcommands.Add(new GetClientCommand());
        Subcommands.Add(new CreateClientCommand());
        Subcommands.Add(new UpdateClientCommand());
        Subcommands.Add(new DeleteClientCommand());
    }
}