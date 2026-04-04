using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Clients.Get;

internal sealed class GetClientHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly GetClientOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public GetClientHandler(
        IShell shell,
        IOptions<GetClientOptions> options,
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
        var (exitCode, response) = await _shell.TryCallAsync(
            ct => _client.GetClientHandlerAsync(_options.ProjectId, _options.Id, ct), cancellationToken);

        if (exitCode != 0)
        {
            return exitCode;
        }

        _shell.RenderDetail(BuildDetail(response!), _hostOptions.OutputFormat);
        return 0;
    }

    private static IReadOnlyList<(string Key, string Value)> BuildDetail(ClientResponse client) =>
    [
        ("Id", client.Id.ToString()),
        ("Project Id", client.ProjectId.ToString()),
        ("Name", client.Name),
        ("Is Active", client.IsActive.ToString()),
        ("Scopes", client.Scopes.Count > 0 ? string.Join(", ", client.Scopes.Select(s => $"{s.Key}={s.Value}")) : string.Empty),
        ("Expires At", client.ExpiresAt?.ToString("O") ?? string.Empty),
        ("Last Used At", client.LastUsedAt?.ToString("O") ?? string.Empty),
        ("Version", client.Version.ToString(CultureInfo.InvariantCulture)),
        ("Created At", client.CreatedAt.ToString("O")),
        ("Updated At", client.UpdatedAt.ToString("O"))
    ];
}