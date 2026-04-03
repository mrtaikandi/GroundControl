using GroundControl.Cli.Shared.Config;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Auth.Status;

internal sealed class StatusHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly CliHostOptions _hostOptions;
    private readonly CredentialStore _store;

    public StatusHandler(
        IShell shell,
        IOptions<CliHostOptions> hostOptions,
        CredentialStore store)
    {
        _shell = shell;
        _hostOptions = hostOptions.Value;
        _store = store;
    }

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var section = await _store.ReadAsync(cancellationToken);

        if (section is null)
        {
            _shell.DisplaySubtleMessage("Not configured. Run 'groundcontrol auth login' to set up credentials.");
            return 0;
        }

        _shell.RenderDetail(CredentialStore.BuildDisplayPairs(section), _hostOptions.OutputFormat);

        return 0;
    }
}