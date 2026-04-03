using GroundControl.Api.Client.Contracts;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Roles.List;

internal sealed class ListRolesHandler : ICommandHandler
{
    private static readonly string[] Headers = ["Id", "Name", "PermissionCount"];

    private static readonly Func<RoleResponse, string>[] ValueExtractors =
    [
        r => r.Id.ToString(),
        r => r.Name,
        r => r.Permissions.Count.ToString()
    ];

    private readonly IShell _shell;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public ListRolesHandler(
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
        var roles = await _client.ListRolesHandlerAsync(cancellationToken);
        _shell.RenderTable((IReadOnlyList<RoleResponse>)roles, Headers, ValueExtractors, _hostOptions.OutputFormat);
        return 0;
    }
}