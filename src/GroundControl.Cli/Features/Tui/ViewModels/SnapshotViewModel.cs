using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class SnapshotViewModel : ReadOnlyResourceViewModel<SnapshotSummaryResponse>
{
    private readonly IGroundControlClient _client;

    public SnapshotViewModel(IGroundControlClient client)
    {
        _client = client;
    }

    internal override string ResourceTypeName => "Snapshot";

    public Guid? ProjectId { get; set; }

    protected override async Task<(IReadOnlyList<SnapshotSummaryResponse> Items, string? NextCursor)> FetchPageAsync(
        string? cursor,
        CancellationToken cancellationToken)
    {
        if (ProjectId is not { } projectId)
        {
            return ([], null);
        }

        var result = await _client.ListSnapshotsHandlerAsync(
            projectId: projectId,
            limit: null,
            after: cursor,
            before: null,
            sortField: null,
            sortOrder: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return (result.Data.ToList(), result.NextCursor);
    }

    internal override string GetDisplayText(SnapshotSummaryResponse item) =>
        $"v{item.SnapshotVersion.ToString(CultureInfo.InvariantCulture)} ({item.EntryCount} entries)";

    internal override IReadOnlyList<DetailPair> GetDetailPairs(SnapshotSummaryResponse item) =>
    [
        new("Id", item.Id.ToString()),
        new("Project Id", item.ProjectId.ToString()),
        new("Version", item.SnapshotVersion.ToString(CultureInfo.InvariantCulture)),
        new("Entry Count", item.EntryCount.ToString(CultureInfo.InvariantCulture)),
        new("Description", item.Description ?? "-"),
        new("Published At", item.PublishedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Published By", item.PublishedBy.ToString())
    ];

    internal override string GetResourceName(SnapshotSummaryResponse item) =>
        $"v{item.SnapshotVersion.ToString(CultureInfo.InvariantCulture)}";

    protected override bool MatchesFilter(SnapshotSummaryResponse item, string filter) =>
        item.SnapshotVersion.ToString(CultureInfo.InvariantCulture).Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        (item.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
}