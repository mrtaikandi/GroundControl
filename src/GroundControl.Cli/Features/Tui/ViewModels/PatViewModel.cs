using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class PatViewModel : ResourceViewModel<PatResponse>
{
    private readonly IGroundControlClient _client;

    public PatViewModel(IGroundControlClient client)
    {
        _client = client;
    }

    protected override async Task<(IReadOnlyList<PatResponse> Items, string? NextCursor)> FetchPageAsync(
        string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await _client.ListPatsHandlerAsync(
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return (result.ToList(), null);
    }

    internal override string GetDisplayText(PatResponse item) =>
        $"{item.Name} ({item.TokenPrefix}...)";

    internal override IReadOnlyList<KeyValuePair<string, string>> GetDetailPairs(PatResponse item) =>
    [
        new("Id", item.Id.ToString()),
        new("Name", item.Name),
        new("Token Prefix", item.TokenPrefix),
        new("Permissions", item.Permissions is { Count: > 0 } ? string.Join(", ", item.Permissions) : "-"),
        new("Is Revoked", (item.IsRevoked ?? false).ToString()),
        new("Expires At", item.ExpiresAt?.ToString("u", CultureInfo.InvariantCulture) ?? "-"),
        new("Last Used At", item.LastUsedAt?.ToString("u", CultureInfo.InvariantCulture) ?? "-"),
        new("Created At", item.CreatedAt.ToString("u", CultureInfo.InvariantCulture))
    ];

    protected override bool MatchesFilter(PatResponse item, string filter) =>
        item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        item.TokenPrefix.Contains(filter, StringComparison.OrdinalIgnoreCase);
}