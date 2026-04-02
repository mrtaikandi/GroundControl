using GroundControl.Cli.Shared.Config;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Config.Show;

internal sealed class ShowConfigHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly CliHostOptions _hostOptions;
    private readonly CredentialStore _store;

    public ShowConfigHandler(
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
            _shell.DisplaySubtleMessage("No local configuration found. Using defaults from appsettings.json.");
            return 0;
        }

        _shell.RenderDetail(CredentialStore.BuildDisplayPairs(section), _hostOptions.OutputFormat);

        return 0;
    }
}