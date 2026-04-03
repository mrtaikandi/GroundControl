using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class TemplateViewModel : ResourceViewModel<TemplateResponse>
{
    private readonly IGroundControlClient _client;

    public TemplateViewModel(IGroundControlClient client)
    {
        _client = client;
    }

    internal override string ResourceTypeName => "Template";

    protected override async Task<(IReadOnlyList<TemplateResponse> Items, string? NextCursor)> FetchPageAsync(
        string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await _client.ListTemplatesHandlerAsync(
            groupId: null,
            globalOnly: null,
            limit: null,
            after: cursor,
            before: null,
            sortField: null,
            sortOrder: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return (result.Data.ToList(), result.NextCursor);
    }

    internal override string GetDisplayText(TemplateResponse item) => item.Name;

    internal override IReadOnlyList<KeyValuePair<string, string>> GetDetailPairs(TemplateResponse item) =>
    [
        new("Id", item.Id.ToString()),
        new("Name", item.Name),
        new("Description", item.Description ?? "-"),
        new("Group Id", item.GroupId?.ToString() ?? "-"),
        new("Version", item.Version.ToString(CultureInfo.InvariantCulture)),
        new("Created At", item.CreatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Created By", item.CreatedBy.ToString()),
        new("Updated At", item.UpdatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Updated By", item.UpdatedBy.ToString())
    ];

    internal override string GetResourceName(TemplateResponse item) => item.Name;

    internal override IReadOnlyList<FieldDefinition> GetFormFields() =>
    [
        new() { Label = "Name", Type = FieldType.Text, IsRequired = true },
        new() { Label = "Description", Type = FieldType.Text },
        new() { Label = "Group Id", Type = FieldType.Text }
    ];

    internal override IReadOnlyList<FieldDefinition> GetEditFormFields(TemplateResponse item) =>
    [
        new() { Label = "Name", Type = FieldType.Text, IsRequired = true, DefaultValue = item.Name },
        new() { Label = "Description", Type = FieldType.Text, DefaultValue = item.Description ?? string.Empty },
        new() { Label = "Group Id", Type = FieldType.Text, DefaultValue = item.GroupId?.ToString() ?? string.Empty }
    ];

    internal override async Task CreateAsync(
        Dictionary<string, string> fieldValues,
        CancellationToken cancellationToken)
    {
        var request = new CreateTemplateRequest
        {
            Name = fieldValues["Name"],
            Description = NullIfEmpty(fieldValues.GetValueOrDefault("Description")),
            GroupId = ParseGuid(fieldValues.GetValueOrDefault("Group Id"))
        };

        await _client.CreateTemplateHandlerAsync(request, cancellationToken).ConfigureAwait(false);
    }

    internal override async Task UpdateAsync(
        TemplateResponse item,
        Dictionary<string, string> fieldValues,
        CancellationToken cancellationToken)
    {
        GroundControlClient.SetIfMatch(item.Version);
        var request = new UpdateTemplateRequest
        {
            Name = fieldValues["Name"],
            Description = NullIfEmpty(fieldValues.GetValueOrDefault("Description")),
            GroupId = ParseGuid(fieldValues.GetValueOrDefault("Group Id"))
        };

        await _client.UpdateTemplateHandlerAsync(item.Id, request, cancellationToken).ConfigureAwait(false);
    }

    internal override async Task DeleteAsync(TemplateResponse item, CancellationToken cancellationToken)
    {
        GroundControlClient.SetIfMatch(item.Version);
        await _client.DeleteTemplateHandlerAsync(item.Id, cancellationToken).ConfigureAwait(false);
    }

    protected override bool MatchesFilter(TemplateResponse item, string filter) =>
        item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        (item.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var guid) ? guid : null;
}