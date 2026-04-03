using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class UserViewModel : ResourceViewModel<UserResponse>
{
    private readonly IGroundControlClient _client;

    public UserViewModel(IGroundControlClient client)
    {
        _client = client;
    }

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

    protected override bool MatchesFilter(UserResponse item, string filter) =>
        item.Username.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        item.Email.Contains(filter, StringComparison.OrdinalIgnoreCase);
}