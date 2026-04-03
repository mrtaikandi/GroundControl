using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.PersonalAccessTokens.Revoke;

internal sealed class RevokeTokenHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly RevokeTokenOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public RevokeTokenHandler(
        IShell shell,
        IOptions<RevokeTokenOptions> options,
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
        PatResponse? current = null;

        if (!_options.Yes && !_hostOptions.NoInteractive)
        {
            try
            {
                current = await _client.GetPatHandlerAsync(_options.Id, cancellationToken);
            }
            catch (GroundControlApiClientException<ProblemDetails> ex)
            {
                _shell.RenderProblemDetails(ex.Result);
                return 1;
            }

            var name = current.Name;
            var confirmed = await _shell.ConfirmAsync(
                $"Revoke token '{name}'?", defaultValue: false, cancellationToken);

            if (!confirmed)
            {
                _shell.DisplaySubtleMessage("Revoke cancelled.");
                return 0;
            }
        }

        try
        {
            await _client.RevokePatHandlerAsync(_options.Id, cancellationToken);
            _shell.DisplaySuccess("Personal access token revoked.");
            return 0;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }
    }
}