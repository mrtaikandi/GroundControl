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

        List<(string Key, string Value)> pairs =
        [
            ("ServerUrl", section["ServerUrl"]?.GetValue<string>() ?? "(not set)")
        ];

        if (section["Auth"] is System.Text.Json.Nodes.JsonObject auth)
        {
            var method = auth["Method"]?.GetValue<string>();
            if (method is not null)
            {
                pairs.Add(("Auth.Method", method));
            }

            var token = auth["Token"]?.GetValue<string>();
            pairs.Add(("Auth.Token", CredentialStore.MaskValue(token)));
        }
        else
        {
            pairs.Add(("Auth", "(none)"));
        }

        _shell.RenderDetail(pairs, _hostOptions.OutputFormat);

        return 0;
    }
}