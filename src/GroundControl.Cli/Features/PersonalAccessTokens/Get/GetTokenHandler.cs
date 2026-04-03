using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.PersonalAccessTokens.Get;

internal sealed class GetTokenHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly GetTokenOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public GetTokenHandler(
        IShell shell,
        IOptions<GetTokenOptions> options,
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
        try
        {
            var token = await _client.GetPatHandlerAsync(_options.Id, cancellationToken);
            _shell.RenderDetail(BuildDetail(token), _hostOptions.OutputFormat);
            return 0;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }
    }

    private static IReadOnlyList<(string Key, string Value)> BuildDetail(PatResponse token) =>
    [
        ("Id", token.Id.ToString()),
        ("Name", token.Name),
        ("Token Prefix", token.TokenPrefix),
        ("Revoked", (token.IsRevoked ?? false).ToString()),
        ("Expires At", token.ExpiresAt?.ToString("O") ?? string.Empty),
        ("Last Used At", token.LastUsedAt?.ToString("O") ?? string.Empty),
        ("Created At", token.CreatedAt.ToString("O"))
    ];
}