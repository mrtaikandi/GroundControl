using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
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
        var version = _options.Version;

        if (version is null)
        {
            try
            {
                var current = await _client.GetScopeHandlerAsync(_options.Id, cancellationToken);
                version = current.Version;
            }
            catch (GroundControlApiClientException<ProblemDetails> ex)
            {
                _shell.RenderProblemDetails(ex.Result);
                return 1;
            }
        }

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

        try
        {
            GroundControlClient.SetIfMatch(version.Value);
            var scope = await _client.UpdateScopeHandlerAsync(_options.Id, request, cancellationToken);
            _shell.DisplaySuccess($"Scope '{scope.Dimension}' updated (version: {scope.Version}).");
            return 0;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex) when (ex.StatusCode == 409)
        {
            var retried = await _shell.HandleConflictAsync(
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
                _hostOptions.NoInteractive,
                cancellationToken);

            return retried ? 0 : 1;
        }
        catch (GroundControlApiClientException<HttpValidationProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }
    }
}