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
        try
        {
            var role = await _client.GetRoleHandlerAsync(_options.Id, cancellationToken);
            _shell.RenderDetail(BuildDetail(role), _hostOptions.OutputFormat);
            return 0;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }
    }

    private static IReadOnlyList<(string Key, string Value)> BuildDetail(RoleResponse role) =>
    [
        ("Id", role.Id.ToString()),
        ("Name", role.Name),
        ("Description", role.Description ?? string.Empty),
        ("Permissions", role.Permissions.Count > 0 ? string.Join(", ", role.Permissions) : string.Empty),
        ("Version", role.Version.ToString()),
        ("Created At", role.CreatedAt.ToString("O")),
        ("Updated At", role.UpdatedAt.ToString("O"))
    ];
}