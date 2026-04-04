using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class ConfigEntryViewModel : ResourceViewModel<ConfigEntryResponse>
{
    private readonly IGroundControlClient _client;

    public ConfigEntryViewModel(IGroundControlClient client)
    {
        _client = client;
    }

    internal override string ResourceTypeName => "Config Entry";

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
        new("Values", ScopedValueFormatter.Format(item.Values)),
        new("Is Sensitive", item.IsSensitive.ToString()),
        new("Description", item.Description ?? "-"),
        new("Version", item.Version.ToString(CultureInfo.InvariantCulture)),
        new("Created At", item.CreatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Created By", item.CreatedBy.ToString()),
        new("Updated At", item.UpdatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Updated By", item.UpdatedBy.ToString())
    ];

    internal override string GetResourceName(ConfigEntryResponse item) => item.Key;

    internal override IReadOnlyList<FieldDefinition> GetFormFields() =>
    [
        new() { Label = "Key", Type = FieldType.Text, IsRequired = true },
        new() { Label = "Owner Id", Type = FieldType.Text, IsRequired = true },
        new() { Label = "Owner Type", Type = FieldType.Text, IsRequired = true },
        new() { Label = "Value Type", Type = FieldType.Text, IsRequired = true },
        new() { Label = "Default Value", Type = FieldType.Text },
        new() { Label = "Is Sensitive", Type = FieldType.Text },
        new() { Label = "Description", Type = FieldType.Text }
    ];

    internal override IReadOnlyList<FieldDefinition> GetEditFormFields(ConfigEntryResponse item) =>
    [
        new() { Label = "Value Type", Type = FieldType.Text, IsRequired = true, DefaultValue = item.ValueType },
        new() { Label = "Default Value", Type = FieldType.Text, DefaultValue = ScopedValueFormatter.Format(item.Values) },
        new() { Label = "Is Sensitive", Type = FieldType.Text, DefaultValue = item.IsSensitive.ToString() },
        new() { Label = "Description", Type = FieldType.Text, DefaultValue = item.Description ?? string.Empty }
    ];

    internal override async Task CreateAsync(Dictionary<string, string> fieldValues, CancellationToken cancellationToken = default)
    {
        var request = new CreateConfigEntryRequest
        {
            Key = fieldValues["Key"],
            OwnerId = Guid.Parse(fieldValues["Owner Id"]),
            OwnerType = Enum.TryParse<ConfigEntryOwnerType>(fieldValues["Owner Type"], true, out var ownerType)
                ? ownerType
                : ConfigEntryOwnerType.Template,
            ValueType = fieldValues["Value Type"],
            IsSensitive = bool.TryParse(fieldValues.GetValueOrDefault("Is Sensitive"), out var isSensitive) ? isSensitive : null,
            Description = NullIfEmpty(fieldValues.GetValueOrDefault("Description")),
            Values = [new ScopedValueRequest { Value = fieldValues.GetValueOrDefault("Default Value") ?? string.Empty }]
        };

        await _client.CreateConfigEntryHandlerAsync(request, cancellationToken).ConfigureAwait(false);
    }

    internal override async Task UpdateAsync(ConfigEntryResponse item, Dictionary<string, string> fieldValues, CancellationToken cancellationToken = default)
    {
        GroundControlClient.SetIfMatch(item.Version);
        var request = new UpdateConfigEntryRequest
        {
            ValueType = fieldValues["Value Type"],
            IsSensitive = bool.TryParse(fieldValues.GetValueOrDefault("Is Sensitive"), out var isSensitive) ? isSensitive : null,
            Description = NullIfEmpty(fieldValues.GetValueOrDefault("Description")),
            Values = [new ScopedValueRequest { Value = fieldValues.GetValueOrDefault("Default Value") ?? string.Empty }]
        };

        await _client.UpdateConfigEntryHandlerAsync(item.Id, request, cancellationToken).ConfigureAwait(false);
    }

    internal override async Task DeleteAsync(ConfigEntryResponse item, CancellationToken cancellationToken = default)
    {
        GroundControlClient.SetIfMatch(item.Version);
        await _client.DeleteConfigEntryHandlerAsync(item.Id, cancellationToken).ConfigureAwait(false);
    }

    protected override bool MatchesFilter(ConfigEntryResponse item, string filter) =>
        item.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        (item.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
}