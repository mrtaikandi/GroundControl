using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class UserViewModel : ResourceViewModel<UserResponse>
{
    private readonly IGroundControlClient _client;

    public UserViewModel(IGroundControlClient client)
    {
        _client = client;
    }

    internal override string ResourceTypeName => "User";

    protected override async Task<(IReadOnlyList<UserResponse> Items, string? NextCursor)> FetchPageAsync(
        string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await _client.ListUsersHandlerAsync(
            limit: null,
            after: cursor,
            before: null,
            sortField: null,
            sortOrder: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return (result.Data.ToList(), result.NextCursor);
    }

    internal override string GetDisplayText(UserResponse item) => item.Username;

    internal override IReadOnlyList<KeyValuePair<string, string>> GetDetailPairs(UserResponse item) =>
    [
        new("Id", item.Id.ToString()),
        new("Username", item.Username),
        new("Email", item.Email),
        new("Is Active", item.IsActive.ToString()),
        new("External Provider", item.ExternalProvider ?? "-"),
        new("Grants", item.Grants.Count > 0
            ? string.Join("; ", item.Grants.Select(g => $"Role={g.RoleId}" + (g.Resource is not null ? $" Resource={g.Resource}" : "")))
            : "-"),
        new("Version", item.Version.ToString(CultureInfo.InvariantCulture)),
        new("Created At", item.CreatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Created By", item.CreatedBy.ToString()),
        new("Updated At", item.UpdatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Updated By", item.UpdatedBy.ToString())
    ];

    internal override string GetResourceName(UserResponse item) => item.Username;

    internal override IReadOnlyList<FieldDefinition> GetFormFields() =>
    [
        new() { Label = "Username", Type = FieldType.Text, IsRequired = true },
        new() { Label = "Email", Type = FieldType.Text, IsRequired = true },
        new() { Label = "Password", Type = FieldType.Text }
    ];

    internal override IReadOnlyList<FieldDefinition> GetEditFormFields(UserResponse item) =>
    [
        new() { Label = "Username", Type = FieldType.Text, IsRequired = true, DefaultValue = item.Username },
        new() { Label = "Email", Type = FieldType.Text, IsRequired = true, DefaultValue = item.Email },
        new() { Label = "Is Active", Type = FieldType.Text, DefaultValue = item.IsActive.ToString() }
    ];

    internal override async Task CreateAsync(Dictionary<string, string> fieldValues, CancellationToken cancellationToken = default)
    {
        var request = new CreateUserRequest
        {
            Username = fieldValues["Username"],
            Email = fieldValues["Email"],
            Password = NullIfEmpty(fieldValues.GetValueOrDefault("Password"))
        };

        await _client.CreateUserHandlerAsync(request, cancellationToken).ConfigureAwait(false);
    }

    internal override async Task UpdateAsync(UserResponse item, Dictionary<string, string> fieldValues, CancellationToken cancellationToken = default)
    {
        GroundControlClient.SetIfMatch(item.Version);
        var request = new UpdateUserRequest
        {
            Username = fieldValues["Username"],
            Email = fieldValues["Email"],
            IsActive = bool.TryParse(fieldValues.GetValueOrDefault("Is Active"), out var isActive) ? isActive : null
        };

        await _client.UpdateUserHandlerAsync(item.Id, request, cancellationToken).ConfigureAwait(false);
    }

    internal override async Task DeleteAsync(UserResponse item, CancellationToken cancellationToken = default)
    {
        GroundControlClient.SetIfMatch(item.Version);
        await _client.DeleteUserHandlerAsync(item.Id, cancellationToken).ConfigureAwait(false);
    }

    protected override bool MatchesFilter(UserResponse item, string filter) =>
        item.Username.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        item.Email.Contains(filter, StringComparison.OrdinalIgnoreCase);
}