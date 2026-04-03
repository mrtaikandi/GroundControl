using System.Text.Json.Nodes;
using GroundControl.Cli.Shared.Config;

namespace GroundControl.Cli.Features.Auth.Logout;

internal sealed class LogoutHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly CredentialStore _store;

    public LogoutHandler(IShell shell, CredentialStore store)
    {
        _shell = shell;
        _store = store;
    }

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var section = await _store.ReadAsync(cancellationToken);

        if (section is null)
        {
            _shell.DisplaySubtleMessage("No credentials found. Already logged out.");
            return 0;
        }

        section.Remove("Auth");

        await _store.WriteAsync(section, cancellationToken);
        _shell.DisplaySuccess("Credentials cleared. Logged out.");

        return 0;
    }
}