using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.ErrorHandling.ConflictRetryHelper;

namespace GroundControl.Cli.Features.ConfigEntries.Delete;

internal sealed class DeleteConfigEntryHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly DeleteConfigEntryOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public DeleteConfigEntryHandler(
        IShell shell,
        IOptions<DeleteConfigEntryOptions> options,
        IOptions<CliHostOptions> hostOptions,
        IGroundControlClient client)
    {
        _shell = shell;
        _options = options.Value;
        _hostOptions = hostOptions.Value;
        _client = client;
    }

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var resolved = await _shell.ResolveVersionAsync(
            _options.Version,
            ct => _client.GetConfigEntryHandlerAsync(_options.Id, decrypt: null, ct),
            r => r.Version,
            cancellationToken);

        if (!resolved.IsSuccess)
        {
            return resolved.ExitCode;
        }

        var version = resolved.Version;
        var current = resolved.Entity;

        if (!_options.Yes && !_hostOptions.NoInteractive)
        {
            var name = current?.Key ?? _options.Id.ToString();
            var confirmed = await _shell.ConfirmAsync(
                $"Delete config entry '{name}'?", defaultValue: false, cancellationToken);

            if (!confirmed)
            {
                _shell.DisplaySubtleMessage("Delete cancelled.");
                return 0;
            }
        }

        return await _shell.TryCallWithConflictHandlingAsync(
            _hostOptions.NoInteractive,
            async ct =>
            {
                GroundControlClient.SetIfMatch(version);
                await _client.DeleteConfigEntryHandlerAsync(_options.Id, ct);
                _shell.DisplaySuccess("Config entry deleted.", _hostOptions.OutputFormat);
                return 0;
            },
            async ct =>
            {
                var latest = await _client.GetConfigEntryHandlerAsync(_options.Id, decrypt: null, ct);
                var diffs = new List<FieldDiff>();

                if (current is not null)
                {
                    if (current.Key != latest.Key)
                    {
                        diffs.Add(new FieldDiff("Key", current.Key, latest.Key));
                    }

                    if (current.ValueType != latest.ValueType)
                    {
                        diffs.Add(new FieldDiff("Value Type", current.ValueType, latest.ValueType));
                    }

                    if (current.IsSensitive != latest.IsSensitive)
                    {
                        diffs.Add(new FieldDiff("Sensitive", current.IsSensitive.ToString(), latest.IsSensitive.ToString()));
                    }

                    if ((current.Description ?? string.Empty) != (latest.Description ?? string.Empty))
                    {
                        diffs.Add(new FieldDiff("Description", current.Description ?? string.Empty, latest.Description ?? string.Empty));
                    }
                }

                return new ConflictInfo(latest.Version, diffs);
            },
            async (newVersion, ct) =>
            {
                GroundControlClient.SetIfMatch(newVersion);
                await _client.DeleteConfigEntryHandlerAsync(_options.Id, ct);
                _shell.DisplaySuccess("Config entry deleted.", _hostOptions.OutputFormat);
            },
            cancellationToken);
    }
}