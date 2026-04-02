using System.Text.Json.Nodes;
using GroundControl.Cli.Shared.Config;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Config.Import;

internal sealed class ImportConfigHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly ImportConfigOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly CredentialStore _store;

    public ImportConfigHandler(
        IShell shell,
        IOptions<ImportConfigOptions> options,
        IOptions<CliHostOptions> hostOptions,
        CredentialStore store)
    {
        _shell = shell;
        _options = options.Value;
        _hostOptions = hostOptions.Value;
        _store = store;
    }

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var json = _options.FilePath is not null
            ? await ReadFromFileAsync(_options.FilePath, cancellationToken)
            : await ReadFromPasteAsync(cancellationToken);

        if (json is null)
        {
            return 1;
        }

        if (!CredentialStore.TryParseConfig(json, out var section, out var error))
        {
            _shell.DisplayError(error!);
            return 1;
        }

        DisplayPreview(section!);

        if (!_options.Yes && !_hostOptions.NoInteractive)
        {
            var confirmed = await _shell.ConfirmAsync("Apply this configuration?", defaultValue: true, cancellationToken);
            if (!confirmed)
            {
                _shell.DisplaySubtleMessage("Import cancelled.");
                return 0;
            }
        }

        await _store.WriteAsync(section!, cancellationToken);
        _shell.DisplaySuccess("Configuration imported successfully.");

        return 0;
    }

    private async Task<string?> ReadFromFileAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            _shell.DisplayError($"File not found: {filePath}");
            return null;
        }

        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }

    private async Task<string?> ReadFromPasteAsync(CancellationToken cancellationToken)
    {
        _shell.DisplayMessage("Paste your JSON configuration below. Enter an empty line to finish:");
        _shell.DisplayEmptyLine();

        var input = await _shell.ReadLinesAsync(cancellationToken);
        if (input is null)
        {
            _shell.DisplayError("No input received.");
        }

        return input;
    }

    private void DisplayPreview(JsonObject section)
    {
        _shell.DisplayEmptyLine();
        _shell.RenderDetail(CredentialStore.BuildDisplayPairs(section), _hostOptions.OutputFormat);
    }
}