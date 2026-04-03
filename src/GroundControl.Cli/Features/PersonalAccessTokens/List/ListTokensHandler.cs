using GroundControl.Api.Client.Contracts;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.PersonalAccessTokens.List;

internal sealed class ListTokensHandler : ICommandHandler
{
    private static readonly string[] Headers = ["Id", "Name", "Token Prefix", "Revoked", "Expires At"];

    private static readonly Func<PatResponse, string>[] ValueExtractors =
    [
        t => t.Id.ToString(),
        t => t.Name,
        t => t.TokenPrefix,
        t => (t.IsRevoked ?? false).ToString(),
        t => t.ExpiresAt?.ToString("O") ?? string.Empty
    ];

    private readonly IShell _shell;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public ListTokensHandler(
        IShell shell,
        IOptions<CliHostOptions> hostOptions,
        IGroundControlClient client)
    {
        _shell = shell;
        _hostOptions = hostOptions.Value;
        _client = client;
    }

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var result = await _client.ListPatsHandlerAsync(cancellationToken);
        _shell.RenderTable<PatResponse>([.. result], Headers, ValueExtractors, _hostOptions.OutputFormat);
        return 0;
    }
}