using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class ClientViewModel : ResourceViewModel<ClientResponse>
{
    private readonly IGroundControlClient _client;
    private Guid? _projectId;

    public ClientViewModel(IGroundControlClient client)
    {
        _client = client;
    }

    public Guid? ProjectId
    {
        get => _projectId;
        set => _projectId = value;
    }

    protected override async Task<(IReadOnlyList<ClientResponse> Items, string? NextCursor)> FetchPageAsync(
        string? cursor,
        CancellationToken cancellationToken)
    {
        if (_projectId is not { } projectId)
        {
            return ([], null);
        }

        var result = await _client.ListClientsHandlerAsync(
            projectId: projectId,
            limit: null,
            after: cursor,
            before: null,
            sortField: null,
            sortOrder: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return (result.Data.ToList(), result.NextCursor);
    }

    internal override string GetDisplayText(ClientResponse item) => item.Name;

    internal override IReadOnlyList<KeyValuePair<string, string>> GetDetailPairs(ClientResponse item) =>
    [
        new("Id", item.Id.ToString()),
        new("Project Id", item.ProjectId.ToString()),
        new("Name", item.Name),
        new("Is Active", item.IsActive.ToString()),
        new("Scopes", item.Scopes.Count > 0
            ? string.Join(", ", item.Scopes.Select(s => $"{s.Key}={s.Value}"))
            : "-"),
        new("Expires At", item.ExpiresAt?.ToString("u", CultureInfo.InvariantCulture) ?? "-"),
        new("Last Used At", item.LastUsedAt?.ToString("u", CultureInfo.InvariantCulture) ?? "-"),
        new("Version", item.Version.ToString(CultureInfo.InvariantCulture)),
        new("Created At", item.CreatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Created By", item.CreatedBy.ToString()),
        new("Updated At", item.UpdatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Updated By", item.UpdatedBy.ToString())
    ];

    protected override bool MatchesFilter(ClientResponse item, string filter) =>
        item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
}