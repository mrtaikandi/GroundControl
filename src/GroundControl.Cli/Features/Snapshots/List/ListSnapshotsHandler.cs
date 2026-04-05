using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.Pagination;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.Pagination.PaginatedRenderer;

namespace GroundControl.Cli.Features.Snapshots.List;

internal sealed class ListSnapshotsHandler : ICommandHandler
{
    private static readonly string[] Headers = ["Id", "Version", "Entries", "Published At", "Description"];

    private static readonly Func<SnapshotSummaryResponse, string>[] ValueExtractors =
    [
        s => s.Id.ToString(),
        s => s.SnapshotVersion.ToString(CultureInfo.InvariantCulture),
        s => s.EntryCount.ToString(CultureInfo.InvariantCulture),
        s => s.PublishedAt.ToString("O"),
        s => s.Description ?? string.Empty
    ];

    private readonly IShell _shell;
    private readonly ListSnapshotsOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public ListSnapshotsHandler(
        IShell shell,
        IOptions<ListSnapshotsOptions> options,
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
        var projectId = _options.ProjectId;

        if (projectId is null && _hostOptions.NoInteractive)
        {
            _shell.DisplayError("Missing required option: --project-id. Provide it explicitly when using --no-interactive.");
            return 1;
        }

        if (projectId is null)
        {
            projectId = await PromptForProjectSelectionAsync(cancellationToken);

            if (projectId is null)
            {
                _shell.DisplayError("No projects available.");
                return 1;
            }
        }

        await _shell.RenderPaginatedTableAsync<SnapshotSummaryResponse>(
            (cursor, ct) => FetchPageAsync(projectId.Value, cursor, ct),
            Headers,
            ValueExtractors,
            _hostOptions.OutputFormat,
            cancellationToken);

        return 0;
    }

    private async Task<Guid?> PromptForProjectSelectionAsync(CancellationToken cancellationToken)
    {
        var projects = new List<ProjectResponse>();
        string? cursor = null;

        do
        {
            var page = await _client.ListProjectsHandlerAsync(
                groupId: null,
                search: null,
                limit: null,
                after: cursor,
                before: null,
                sortField: null,
                sortOrder: null,
                cancellationToken: cancellationToken);

            projects.AddRange(page.Data);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        if (projects.Count == 0)
        {
            return null;
        }

        var selected = await _shell.PromptForSelectionAsync(
            "Select project:",
            projects,
            p => $"{p.Name} ({p.Id})",
            enableSearch: true,
            cancellationToken: cancellationToken);

        return selected.Id;
    }

    private async Task<Page<SnapshotSummaryResponse>> FetchPageAsync(
        Guid projectId,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await _client.ListSnapshotsHandlerAsync(
            projectId: projectId,
            limit: null,
            after: cursor,
            before: null,
            sortField: null,
            sortOrder: null,
            cancellationToken: cancellationToken);

        return new Page<SnapshotSummaryResponse>((IReadOnlyList<SnapshotSummaryResponse>)result.Data, result.NextCursor);
    }
}