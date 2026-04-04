using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Scopes.Get;

internal sealed class GetScopeHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly GetScopeOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public GetScopeHandler(
        IShell shell,
        IOptions<GetScopeOptions> options,
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
        var (exitCode, scope) = await _shell.TryCallAsync(
            ct => _client.GetScopeHandlerAsync(_options.Id, ct), cancellationToken);

        if (exitCode != 0)
        {
            return exitCode;
        }

        _shell.RenderDetail(BuildDetail(scope!), _hostOptions.OutputFormat);
        return 0;
    }

    private static IReadOnlyList<(string Key, string Value)> BuildDetail(ScopeResponse scope) =>
    [
        ("Id", scope.Id.ToString()),
        ("Dimension", scope.Dimension),
        ("Allowed Values", string.Join(", ", scope.AllowedValues)),
        ("Description", scope.Description ?? string.Empty),
        ("Version", scope.Version.ToString(CultureInfo.InvariantCulture)),
        ("Created At", scope.CreatedAt.ToString("O")),
        ("Updated At", scope.UpdatedAt.ToString("O"))
    ];
}