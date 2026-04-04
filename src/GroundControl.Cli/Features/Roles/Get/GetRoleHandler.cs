using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Roles.Get;

internal sealed class GetRoleHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly GetRoleOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public GetRoleHandler(
        IShell shell,
        IOptions<GetRoleOptions> options,
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
        var (exitCode, role) = await _shell.TryCallAsync(
            ct => _client.GetRoleHandlerAsync(_options.Id, ct), cancellationToken);

        if (exitCode != 0)
        {
            return exitCode;
        }

        _shell.RenderDetail(BuildDetail(role!), _hostOptions.OutputFormat);
        return 0;
    }

    private static IReadOnlyList<(string Key, string Value)> BuildDetail(RoleResponse role) =>
    [
        ("Id", role.Id.ToString()),
        ("Name", role.Name),
        ("Description", role.Description ?? string.Empty),
        ("Permissions", role.Permissions.Count > 0 ? string.Join(", ", role.Permissions) : string.Empty),
        ("Version", role.Version.ToString(CultureInfo.InvariantCulture)),
        ("Created At", role.CreatedAt.ToString("O")),
        ("Updated At", role.UpdatedAt.ToString("O"))
    ];
}