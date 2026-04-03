using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Tui.Views;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class GroupViewModel : ResourceViewModel<GroupResponse>
{
    private readonly IGroundControlClient _client;

    public GroupViewModel(IGroundControlClient client)
    {
        _client = client;
    }

    internal override string ResourceTypeName => "Group";

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

        return (result.Data.ToList(), result.NextCursor);
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

    internal override string GetResourceName(GroupResponse item) => item.Name;

    internal override IReadOnlyList<FieldDefinition> GetFormFields() =>
    [
        new() { Label = "Name", Type = FieldType.Text, IsRequired = true },
        new() { Label = "Description", Type = FieldType.Text }
    ];

    internal override IReadOnlyList<FieldDefinition> GetEditFormFields(GroupResponse item) =>
    [
        new() { Label = "Name", Type = FieldType.Text, IsRequired = true, DefaultValue = item.Name },
        new() { Label = "Description", Type = FieldType.Text, DefaultValue = item.Description ?? string.Empty }
    ];

    internal override async Task CreateAsync(
        Dictionary<string, string> fieldValues,
        CancellationToken cancellationToken)
    {
        var request = new CreateGroupRequest
        {
            Name = fieldValues["Name"],
            Description = NullIfEmpty(fieldValues.GetValueOrDefault("Description"))
        };

        await _client.CreateGroupHandlerAsync(request, cancellationToken).ConfigureAwait(false);
    }

    internal override async Task UpdateAsync(
        GroupResponse item,
        Dictionary<string, string> fieldValues,
        CancellationToken cancellationToken)
    {
        GroundControlClient.SetIfMatch(item.Version);
        var request = new UpdateGroupRequest
        {
            Name = fieldValues["Name"],
            Description = NullIfEmpty(fieldValues.GetValueOrDefault("Description"))
        };

        await _client.UpdateGroupHandlerAsync(item.Id, request, cancellationToken).ConfigureAwait(false);
    }

    internal override async Task DeleteAsync(GroupResponse item, CancellationToken cancellationToken)
    {
        GroundControlClient.SetIfMatch(item.Version);
        await _client.DeleteGroupHandlerAsync(item.Id, cancellationToken).ConfigureAwait(false);
    }

    protected override bool MatchesFilter(GroupResponse item, string filter) =>
        item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        (item.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}