using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Groups.Get;

internal sealed class GetGroupHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly GetGroupOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public GetGroupHandler(
        IShell shell,
        IOptions<GetGroupOptions> options,
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
            var group = await _client.GetGroupHandlerAsync(_options.Id, cancellationToken);
            _shell.RenderDetail(BuildDetail(group), _hostOptions.OutputFormat);
            return 0;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }
    }

    private static IReadOnlyList<(string Key, string Value)> BuildDetail(GroupResponse group) =>
    [
        ("Id", group.Id.ToString()),
        ("Name", group.Name),
        ("Description", group.Description ?? string.Empty),
        ("Version", group.Version.ToString(CultureInfo.InvariantCulture)),
        ("Created At", group.CreatedAt.ToString("O")),
        ("Updated At", group.UpdatedAt.ToString("O"))
    ];
}