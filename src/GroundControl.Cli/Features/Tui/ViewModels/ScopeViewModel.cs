using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Tui.Views;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class ScopeViewModel : ResourceViewModel<ScopeResponse>
{
    private readonly IGroundControlClient _client;

    public ScopeViewModel(IGroundControlClient client)
    {
        _client = client;
    }

    internal override string ResourceTypeName => "Scope";

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

        return (result.Data.ToList(), result.NextCursor);
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

    internal override string GetResourceName(ScopeResponse item) => item.Dimension;

    internal override IReadOnlyList<FieldDefinition> GetFormFields() =>
    [
        new() { Label = "Dimension", Type = FieldType.Text, IsRequired = true },
        new() { Label = "Allowed Values", Type = FieldType.Text, IsRequired = true },
        new() { Label = "Description", Type = FieldType.Text }
    ];

    internal override IReadOnlyList<FieldDefinition> GetEditFormFields(ScopeResponse item) =>
    [
        new() { Label = "Dimension", Type = FieldType.Text, IsRequired = true, DefaultValue = item.Dimension },
        new() { Label = "Allowed Values", Type = FieldType.Text, IsRequired = true, DefaultValue = string.Join(", ", item.AllowedValues) },
        new() { Label = "Description", Type = FieldType.Text, DefaultValue = item.Description ?? string.Empty }
    ];

    internal override async Task CreateAsync(
        Dictionary<string, string> fieldValues,
        CancellationToken cancellationToken)
    {
        var request = new CreateScopeRequest
        {
            Dimension = fieldValues["Dimension"],
            AllowedValues = ParseCommaSeparated(fieldValues["Allowed Values"]),
            Description = NullIfEmpty(fieldValues.GetValueOrDefault("Description"))
        };

        await _client.CreateScopeHandlerAsync(request, cancellationToken).ConfigureAwait(false);
    }

    internal override async Task UpdateAsync(
        ScopeResponse item,
        Dictionary<string, string> fieldValues,
        CancellationToken cancellationToken)
    {
        GroundControlClient.SetIfMatch(item.Version);
        var request = new UpdateScopeRequest
        {
            Dimension = fieldValues["Dimension"],
            AllowedValues = ParseCommaSeparated(fieldValues["Allowed Values"]),
            Description = NullIfEmpty(fieldValues.GetValueOrDefault("Description"))
        };

        await _client.UpdateScopeHandlerAsync(item.Id, request, cancellationToken).ConfigureAwait(false);
    }

    internal override async Task DeleteAsync(ScopeResponse item, CancellationToken cancellationToken)
    {
        GroundControlClient.SetIfMatch(item.Version);
        await _client.DeleteScopeHandlerAsync(item.Id, cancellationToken).ConfigureAwait(false);
    }

    protected override bool MatchesFilter(ScopeResponse item, string filter) =>
        item.Dimension.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        (item.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);

    private static List<string> ParseCommaSeparated(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}