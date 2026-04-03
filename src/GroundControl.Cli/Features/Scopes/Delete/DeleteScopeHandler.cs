using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.ErrorHandling.ConflictRetryHelper;

namespace GroundControl.Cli.Features.Scopes.Delete;

internal sealed class DeleteScopeHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly DeleteScopeOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public DeleteScopeHandler(
        IShell shell,
        IOptions<DeleteScopeOptions> options,
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
        ScopeResponse? current = null;

        if (version is null)
        {
            try
            {
                current = await _client.GetScopeHandlerAsync(_options.Id, cancellationToken);
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
            var name = current?.Dimension ?? _options.Id.ToString();
            var confirmed = await _shell.ConfirmAsync(
                $"Delete scope '{name}'?", defaultValue: false, cancellationToken);

            if (!confirmed)
            {
                _shell.DisplaySubtleMessage("Delete cancelled.");
                return 0;
            }
        }

        try
        {
            GroundControlClient.SetIfMatch(version.Value);
            await _client.DeleteScopeHandlerAsync(_options.Id, cancellationToken);
            _shell.DisplaySuccess("Scope deleted.");
            return 0;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex) when (ex.StatusCode == 409)
        {
            var retried = await _shell.HandleConflictAsync(
                async ct =>
                {
                    var latest = await _client.GetScopeHandlerAsync(_options.Id, ct);
                    var diffs = new List<FieldDiff>();

                    if (current is not null)
                    {
                        if (current.Dimension != latest.Dimension)
                        {
                            diffs.Add(new FieldDiff("Dimension", current.Dimension, latest.Dimension));
                        }

                        var currentValues = string.Join(", ", current.AllowedValues);
                        var latestValues = string.Join(", ", latest.AllowedValues);
                        if (currentValues != latestValues)
                        {
                            diffs.Add(new FieldDiff("Allowed Values", currentValues, latestValues));
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
                    await _client.DeleteScopeHandlerAsync(_options.Id, ct);
                    _shell.DisplaySuccess("Scope deleted.");
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