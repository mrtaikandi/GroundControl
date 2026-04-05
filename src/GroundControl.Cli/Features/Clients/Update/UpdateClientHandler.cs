using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.ErrorHandling.ConflictRetryHelper;

namespace GroundControl.Cli.Features.Clients.Update;

internal sealed class UpdateClientHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly UpdateClientOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public UpdateClientHandler(
        IShell shell,
        IOptions<UpdateClientOptions> options,
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
        // Always fetch current state to fill in defaults for non-provided fields,
        // since UpdateClientRequest.IsActive is non-nullable and cannot be omitted.
        var resolved = await _shell.ResolveVersionAsync(
            null,
            ct => _client.GetClientHandlerAsync(_options.ProjectId, _options.Id, ct),
            r => r.Version,
            cancellationToken);

        if (!resolved.IsSuccess)
        {
            return resolved.ExitCode;
        }

        var version = _options.Version ?? resolved.Version;
        var current = resolved.Entity!;

        var request = new UpdateClientRequest
        {
            Name = _options.Name ?? current.Name,
            IsActive = _options.IsActive ?? current.IsActive,
            ExpiresAt = _options.ExpiresAt ?? current.ExpiresAt
        };

        return await _shell.TryCallWithConflictHandlingAsync(
            _hostOptions.NoInteractive,
            async ct =>
            {
                GroundControlClient.SetIfMatch(version);
                var response = await _client.UpdateClientHandlerAsync(_options.ProjectId, _options.Id, request, ct);
                _shell.DisplaySuccess(response, _hostOptions.OutputFormat,
                    r => $"Client '{r.Name}' updated (version: {r.Version}).");
                return 0;
            },
            async ct =>
            {
                var current = await _client.GetClientHandlerAsync(_options.ProjectId, _options.Id, ct);
                var diffs = new List<FieldDiff>();

                if (_options.Name is not null && _options.Name != current.Name)
                {
                    diffs.Add(new FieldDiff("Name", _options.Name, current.Name));
                }

                if (_options.IsActive is not null && _options.IsActive != current.IsActive)
                {
                    diffs.Add(new FieldDiff("Is Active", _options.IsActive.Value.ToString(), current.IsActive.ToString()));
                }

                if (_options.ExpiresAt is not null && _options.ExpiresAt != current.ExpiresAt)
                {
                    diffs.Add(new FieldDiff("Expires At", _options.ExpiresAt.Value.ToString("O"), current.ExpiresAt?.ToString("O") ?? string.Empty));
                }

                return new ConflictInfo(current.Version, diffs);
            },
            async (newVersion, ct) =>
            {
                GroundControlClient.SetIfMatch(newVersion);
                var response = await _client.UpdateClientHandlerAsync(_options.ProjectId, _options.Id, request, ct);
                _shell.DisplaySuccess(response, _hostOptions.OutputFormat,
                    r => $"Client '{r.Name}' updated (version: {r.Version}).");
            },
            cancellationToken);
    }
}