using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class AuditViewModel : ResourceViewModel<AuditRecordResponse>
{
    private readonly IGroundControlClient _client;

    public AuditViewModel(IGroundControlClient client)
    {
        _client = client;
    }

    protected override async Task<(IReadOnlyList<AuditRecordResponse> Items, string? NextCursor)> FetchPageAsync(
        string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await _client.ListAuditRecordsHandlerAsync(
            entityType: null,
            entityId: null,
            performedBy: null,
            from: null,
            to: null,
            after: cursor,
            before: null,
            limit: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return (result.Data.ToList(), result.NextCursor);
    }

    internal override string GetDisplayText(AuditRecordResponse item) =>
        $"{item.Action} {item.EntityType} ({item.PerformedAt.ToString("u", CultureInfo.InvariantCulture)})";

    internal override IReadOnlyList<KeyValuePair<string, string>> GetDetailPairs(AuditRecordResponse item)
    {
        var pairs = new List<KeyValuePair<string, string>>
        {
            new("Id", item.Id.ToString()),
            new("Entity Type", item.EntityType),
            new("Entity Id", item.EntityId.ToString()),
            new("Group Id", item.GroupId?.ToString() ?? "-"),
            new("Action", item.Action),
            new("Performed By", item.PerformedBy.ToString()),
            new("Performed At", item.PerformedAt.ToString("u", CultureInfo.InvariantCulture))
        };

        if (item.Changes.Count > 0)
        {
            pairs.Add(new KeyValuePair<string, string>("Changes", string.Empty));
            pairs.AddRange(item.Changes.Select(change => new KeyValuePair<string, string>($"  {change.Field}", $"{change.OldValue ?? "(null)"} → {change.NewValue ?? "(null)"}")));
        }

        if (item.Metadata is { Count: > 0 })
        {
            pairs.Add(new KeyValuePair<string, string>("Metadata", string.Empty));
            pairs.AddRange(item.Metadata.Select(entry => new KeyValuePair<string, string>($"  {entry.Key}", entry.Value)));
        }

        return pairs;
    }

    protected override bool MatchesFilter(AuditRecordResponse item, string filter) =>
        item.EntityType.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        item.Action.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        item.EntityId.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase);
}