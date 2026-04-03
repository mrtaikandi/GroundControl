using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class ConfigEntryViewModel : ResourceViewModel<ConfigEntryResponse>
{
    private readonly IGroundControlClient _client;

    public ConfigEntryViewModel(IGroundControlClient client)
    {
        _client = client;
    }

    protected override async Task<(IReadOnlyList<ConfigEntryResponse> Items, string? NextCursor)> FetchPageAsync(
        string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await _client.ListConfigEntriesHandlerAsync(
            ownerId: null,
            ownerType: null,
            keyPrefix: null,
            limit: null,
            after: cursor,
            before: null,
            sortField: null,
            sortOrder: null,
            decrypt: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return (result.Data.ToList(), result.NextCursor);
    }

    internal override string GetDisplayText(ConfigEntryResponse item) => item.Key;

    internal override IReadOnlyList<KeyValuePair<string, string>> GetDetailPairs(ConfigEntryResponse item) =>
    [
        new("Id", item.Id.ToString()),
        new("Key", item.Key),
        new("Owner Id", item.OwnerId.ToString()),
        new("Owner Type", item.OwnerType.ToString()),
        new("Value Type", item.ValueType),
        new("Values", FormatScopedValues(item.Values)),
        new("Is Sensitive", item.IsSensitive.ToString()),
        new("Description", item.Description ?? "-"),
        new("Version", item.Version.ToString(CultureInfo.InvariantCulture)),
        new("Created At", item.CreatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Created By", item.CreatedBy.ToString()),
        new("Updated At", item.UpdatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Updated By", item.UpdatedBy.ToString())
    ];

    protected override bool MatchesFilter(ConfigEntryResponse item, string filter) =>
        item.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        (item.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);

    private static string FormatScopedValues(ICollection<ScopedValue> values)
    {
        if (values.Count == 0)
        {
            return "-";
        }

        return string.Join("; ", values.Select(v =>
        {
            var scope = v.Scopes is { Count: > 0 }
                ? string.Join(", ", v.Scopes.Select(s => $"{s.Key}={s.Value}"))
                : "default";
            return $"[{scope}] {v.Value}";
        }));
    }
}