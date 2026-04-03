using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Projects.Get;

internal sealed class GetProjectHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly GetProjectOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public GetProjectHandler(
        IShell shell,
        IOptions<GetProjectOptions> options,
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
            var project = await _client.GetProjectHandlerAsync(_options.Id, cancellationToken);
            _shell.RenderDetail(BuildDetail(project), _hostOptions.OutputFormat);
            return 0;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }
    }

    private static IReadOnlyList<(string Key, string Value)> BuildDetail(ProjectResponse project) =>
    [
        ("Id", project.Id.ToString()),
        ("Name", project.Name),
        ("Description", project.Description ?? string.Empty),
        ("Group Id", project.GroupId?.ToString() ?? string.Empty),
        ("Template Ids", project.TemplateIds.Count > 0 ? string.Join(", ", project.TemplateIds) : string.Empty),
        ("Active Snapshot Id", project.ActiveSnapshotId?.ToString() ?? string.Empty),
        ("Version", project.Version.ToString(CultureInfo.InvariantCulture)),
        ("Created At", project.CreatedAt.ToString("O")),
        ("Updated At", project.UpdatedAt.ToString("O"))
    ];
}