using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.ErrorHandling.ConflictRetryHelper;

namespace GroundControl.Cli.Features.Scopes.Update;

internal sealed class UpdateScopeHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly UpdateScopeOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public UpdateScopeHandler(
        IShell shell,
        IOptions<UpdateScopeOptions> options,
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
            ct => _client.GetScopeHandlerAsync(_options.Id, ct),
            r => r.Version,
            cancellationToken);

        if (!resolved.IsSuccess)
        {
            return resolved.ExitCode;
        }

        var version = resolved.Version;

        var allowedValues = _options.Values?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // WhenWritingNull serializer policy omits null fields from the JSON body,
        // so only fields the user explicitly provided are sent to the API.
        var request = new UpdateScopeRequest
        {
            Dimension = _options.Dimension!,
            AllowedValues = allowedValues!,
            Description = _options.Description
        };

        return await _shell.TryCallWithConflictHandlingAsync(
            _hostOptions.NoInteractive,
            async ct =>
            {
                GroundControlClient.SetIfMatch(version);
                var scope = await _client.UpdateScopeHandlerAsync(_options.Id, request, ct);
                _shell.DisplaySuccess($"Scope '{scope.Dimension}' updated (version: {scope.Version}).");
                return 0;
            },
            async ct =>
            {
                var current = await _client.GetScopeHandlerAsync(_options.Id, ct);
                var diffs = new List<FieldDiff>();

                if (_options.Dimension is not null && _options.Dimension != current.Dimension)
                {
                    diffs.Add(new FieldDiff("Dimension", _options.Dimension, current.Dimension));
                }

                if (allowedValues is not null)
                {
                    var currentValues = string.Join(", ", current.AllowedValues);
                    var requestedValues = string.Join(", ", allowedValues);
                    if (requestedValues != currentValues)
                    {
                        diffs.Add(new FieldDiff("Allowed Values", requestedValues, currentValues));
                    }
                }

                if (_options.Description is not null && _options.Description != (current.Description ?? string.Empty))
                {
                    diffs.Add(new FieldDiff("Description", _options.Description, current.Description ?? string.Empty));
                }

                return new ConflictInfo(current.Version, diffs);
            },
            async (newVersion, ct) =>
            {
                GroundControlClient.SetIfMatch(newVersion);
                var scope = await _client.UpdateScopeHandlerAsync(_options.Id, request, ct);
                _shell.DisplaySuccess($"Scope '{scope.Dimension}' updated (version: {scope.Version}).");
            },
            cancellationToken);
    }
}