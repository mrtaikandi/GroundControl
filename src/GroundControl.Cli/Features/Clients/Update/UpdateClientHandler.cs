using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
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
        var version = _options.Version;
        ClientResponse? current = null;

        // Always fetch current state to fill in defaults for non-provided fields,
        // since UpdateClientRequest.IsActive is non-nullable and cannot be omitted.
        try
        {
            current = await _client.GetClientHandlerAsync(_options.ProjectId, _options.Id, cancellationToken);
            version ??= current.Version;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }

        var request = new UpdateClientRequest
        {
            Name = _options.Name ?? current.Name,
            IsActive = _options.IsActive ?? current.IsActive,
            ExpiresAt = _options.ExpiresAt ?? current.ExpiresAt
        };

        try
        {
            GroundControlClient.SetIfMatch(version.Value);
            var response = await _client.UpdateClientHandlerAsync(_options.ProjectId, _options.Id, request, cancellationToken);
            _shell.DisplaySuccess($"Client '{response.Name}' updated (version: {response.Version}).");
            return 0;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex) when (ex.StatusCode == 409)
        {
            var retried = await _shell.HandleConflictAsync(
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
                    _shell.DisplaySuccess($"Client '{response.Name}' updated (version: {response.Version}).");
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