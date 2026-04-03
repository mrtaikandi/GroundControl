using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Users.Get;

internal sealed class GetUserHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly GetUserOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public GetUserHandler(
        IShell shell,
        IOptions<GetUserOptions> options,
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
            var user = await _client.GetUserHandlerAsync(_options.Id, cancellationToken);
            _shell.RenderDetail(BuildDetail(user), _hostOptions.OutputFormat);
            return 0;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }
    }

    private static IReadOnlyList<(string Key, string Value)> BuildDetail(UserResponse user) =>
    [
        ("Id", user.Id.ToString()),
        ("Username", user.Username),
        ("Email", user.Email),
        ("Active", user.IsActive.ToString()),
        ("External Provider", user.ExternalProvider ?? string.Empty),
        ("Grants", user.Grants.Count > 0 ? string.Join(", ", user.Grants.Select(g => g.RoleId.ToString())) : string.Empty),
        ("Version", user.Version.ToString(CultureInfo.InvariantCulture)),
        ("Created At", user.CreatedAt.ToString("O")),
        ("Updated At", user.UpdatedAt.ToString("O"))
    ];
}