using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class GroupViewModel : ResourceViewModel<GroupResponse>
{
    private readonly IGroundControlClient _client;

    public GroupViewModel(IGroundControlClient client)
    {
        _client = client;
    }

    protected override async Task<(IReadOnlyList<GroupResponse> Items, string? NextCursor)> FetchPageAsync(
        string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await _client.ListGroupsHandlerAsync(
            limit: null,
            after: cursor,
            before: null,
            sortField: null,
            sortOrder: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ((IReadOnlyList<GroupResponse>)result.Data, result.NextCursor);
    }

    internal override string GetDisplayText(GroupResponse item) => item.Name;

    internal override IReadOnlyList<KeyValuePair<string, string>> GetDetailPairs(GroupResponse item) =>
    [
        new("Id", item.Id.ToString()),
        new("Name", item.Name),
        new("Description", item.Description ?? "-"),
        new("Version", item.Version.ToString(CultureInfo.InvariantCulture)),
        new("Created At", item.CreatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Created By", item.CreatedBy.ToString()),
        new("Updated At", item.UpdatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Updated By", item.UpdatedBy.ToString())
    ];

    protected override bool MatchesFilter(GroupResponse item, string filter) =>
        item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        (item.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
}