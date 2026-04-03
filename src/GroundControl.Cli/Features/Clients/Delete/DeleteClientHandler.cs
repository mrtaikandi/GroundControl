using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.ErrorHandling.ConflictRetryHelper;

namespace GroundControl.Cli.Features.Clients.Delete;

internal sealed class DeleteClientHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly DeleteClientOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public DeleteClientHandler(
        IShell shell,
        IOptions<DeleteClientOptions> options,
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

        if (version is null)
        {
            try
            {
                current = await _client.GetClientHandlerAsync(_options.ProjectId, _options.Id, cancellationToken);
                version = current.Version;
            }
            catch (GroundControlApiClientException<ProblemDetails> ex)
            {
                _shell.RenderProblemDetails(ex.Result);
                return 1;
            }
        }

        if (!_options.Yes && !_hostOptions.NoInteractive)
        {
            var name = current?.Name ?? _options.Id.ToString();
            var confirmed = await _shell.ConfirmAsync(
                $"Delete client '{name}'?", defaultValue: false, cancellationToken);

            if (!confirmed)
            {
                _shell.DisplaySubtleMessage("Delete cancelled.");
                return 0;
            }
        }

        try
        {
            GroundControlClient.SetIfMatch(version.Value);
            await _client.DeleteClientHandlerAsync(_options.ProjectId, _options.Id, cancellationToken);
            _shell.DisplaySuccess("Client deleted.");
            return 0;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex) when (ex.StatusCode == 409)
        {
            var retried = await _shell.HandleConflictAsync(
                async ct =>
                {
                    var latest = await _client.GetClientHandlerAsync(_options.ProjectId, _options.Id, ct);
                    var diffs = new List<FieldDiff>();

                    if (current is not null)
                    {
                        if (current.Name != latest.Name)
                        {
                            diffs.Add(new FieldDiff("Name", current.Name, latest.Name));
                        }

                        if (current.IsActive != latest.IsActive)
                        {
                            diffs.Add(new FieldDiff("Is Active", current.IsActive.ToString(), latest.IsActive.ToString()));
                        }

                        if (current.ExpiresAt != latest.ExpiresAt)
                        {
                            diffs.Add(new FieldDiff("Expires At", current.ExpiresAt?.ToString("O") ?? string.Empty, latest.ExpiresAt?.ToString("O") ?? string.Empty));
                        }
                    }

                    return new ConflictInfo(latest.Version, diffs);
                },
                async (newVersion, ct) =>
                {
                    GroundControlClient.SetIfMatch(newVersion);
                    await _client.DeleteClientHandlerAsync(_options.ProjectId, _options.Id, ct);
                    _shell.DisplaySuccess("Client deleted.");
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