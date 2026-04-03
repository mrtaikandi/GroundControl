using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.Pagination;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.Pagination.PaginatedRenderer;

namespace GroundControl.Cli.Features.Projects.List;

internal sealed class ListProjectsHandler : ICommandHandler
{
    private static readonly string[] Headers = ["Id", "Name", "GroupId", "TemplateCount"];

    private static readonly Func<ProjectResponse, string>[] ValueExtractors =
    [
        p => p.Id.ToString(),
        p => p.Name,
        p => p.GroupId?.ToString() ?? string.Empty,
        p => p.TemplateIds.Count.ToString(CultureInfo.InvariantCulture)
    ];

    private readonly IShell _shell;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public ListProjectsHandler(
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
        await _shell.RenderPaginatedTableAsync<ProjectResponse>(
            FetchPageAsync,
            Headers,
            ValueExtractors,
            _hostOptions.OutputFormat,
            cancellationToken);

        return 0;
    }

    private async Task<Page<ProjectResponse>> FetchPageAsync(string? cursor, CancellationToken cancellationToken)
    {
        var result = await _client.ListProjectsHandlerAsync(
            groupId: null,
            search: null,
            limit: null,
            after: cursor,
            before: null,
            sortField: null,
            sortOrder: null,
            cancellationToken: cancellationToken);

        return new Page<ProjectResponse>((IReadOnlyList<ProjectResponse>)result.Data, result.NextCursor);
    }
}