using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class ScopeViewModel : ResourceViewModel<ScopeResponse>
{
    private readonly IGroundControlClient _client;

    public ScopeViewModel(IGroundControlClient client)
    {
        _client = client;
    }

    protected override async Task<(IReadOnlyList<ScopeResponse> Items, string? NextCursor)> FetchPageAsync(
        string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await _client.ListScopesHandlerAsync(
            limit: null,
            after: cursor,
            before: null,
            sortField: null,
            sortOrder: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ((IReadOnlyList<ScopeResponse>)result.Data, result.NextCursor);
    }

    internal override string GetDisplayText(ScopeResponse item) => item.Dimension;

    internal override IReadOnlyList<KeyValuePair<string, string>> GetDetailPairs(ScopeResponse item) =>
    [
        new("Id", item.Id.ToString()),
        new("Dimension", item.Dimension),
        new("Allowed Values", string.Join(", ", item.AllowedValues)),
        new("Description", item.Description ?? "-"),
        new("Version", item.Version.ToString(CultureInfo.InvariantCulture)),
        new("Created At", item.CreatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Created By", item.CreatedBy.ToString()),
        new("Updated At", item.UpdatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Updated By", item.UpdatedBy.ToString())
    ];

    protected override bool MatchesFilter(ScopeResponse item, string filter) =>
        item.Dimension.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        (item.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
}