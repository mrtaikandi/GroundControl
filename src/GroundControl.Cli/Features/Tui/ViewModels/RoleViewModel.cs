using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class RoleViewModel : ResourceViewModel<RoleResponse>
{
    private readonly IGroundControlClient _client;

    public RoleViewModel(IGroundControlClient client)
    {
        _client = client;
    }

    internal override string ResourceTypeName => "Role";

    protected override async Task<(IReadOnlyList<RoleResponse> Items, string? NextCursor)> FetchPageAsync(
        string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await _client.ListRolesHandlerAsync(
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return (result.ToList(), null);
    }

    internal override string GetDisplayText(RoleResponse item) => item.Name;

    internal override IReadOnlyList<DetailPair> GetDetailPairs(RoleResponse item) =>
    [
        new("Id", item.Id.ToString()),
        new("Name", item.Name),
        new("Description", item.Description ?? "-"),
        new("Permissions", item.Permissions.Count > 0 ? string.Join(", ", item.Permissions) : "-"),
        .. GetStandardMetadataPairs(new(item.Version, item.CreatedAt, item.CreatedBy, item.UpdatedAt, item.UpdatedBy))
    ];

    internal override string GetResourceName(RoleResponse item) => item.Name;

    internal override IReadOnlyList<FieldDefinition> GetFormFields() =>
    [
        new() { Label = "Name", Type = FieldType.Text, IsRequired = true },
        new() { Label = "Description", Type = FieldType.Text },
        new() { Label = "Permissions", Type = FieldType.Text }
    ];

    internal override IReadOnlyList<FieldDefinition> GetEditFormFields(RoleResponse item) =>
    [
        new() { Label = "Name", Type = FieldType.Text, IsRequired = true, DefaultValue = item.Name },
        new() { Label = "Description", Type = FieldType.Text, DefaultValue = item.Description ?? string.Empty },
        new() { Label = "Permissions", Type = FieldType.Text, DefaultValue = item.Permissions.Count > 0 ? string.Join(", ", item.Permissions) : string.Empty }
    ];

    internal override async Task CreateAsync(Dictionary<string, string> fieldValues, CancellationToken cancellationToken = default)
    {
        var request = new CreateRoleRequest
        {
            Name = fieldValues["Name"],
            Description = NullIfEmpty(fieldValues.GetValueOrDefault("Description")),
            Permissions = ParseCommaSeparated(fieldValues.GetValueOrDefault("Permissions"))
        };

        await _client.CreateRoleHandlerAsync(request, cancellationToken).ConfigureAwait(false);
    }

    internal override async Task UpdateAsync(RoleResponse item, Dictionary<string, string> fieldValues, CancellationToken cancellationToken = default)
    {
        GroundControlClient.SetIfMatch(item.Version);
        var request = new UpdateRoleRequest
        {
            Name = fieldValues["Name"],
            Description = NullIfEmpty(fieldValues.GetValueOrDefault("Description")),
            Permissions = ParseCommaSeparated(fieldValues.GetValueOrDefault("Permissions"))
        };

        await _client.UpdateRoleHandlerAsync(item.Id, request, cancellationToken).ConfigureAwait(false);
    }

    internal override async Task DeleteAsync(RoleResponse item, CancellationToken cancellationToken = default)
    {
        GroundControlClient.SetIfMatch(item.Version);
        await _client.DeleteRoleHandlerAsync(item.Id, cancellationToken).ConfigureAwait(false);
    }

    protected override bool MatchesFilter(RoleResponse item, string filter) =>
        item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        (item.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);

}