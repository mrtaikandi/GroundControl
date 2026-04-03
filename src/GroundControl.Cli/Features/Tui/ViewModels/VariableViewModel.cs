using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class VariableViewModel : ResourceViewModel<VariableResponse>
{
    private readonly IGroundControlClient _client;

    public VariableViewModel(IGroundControlClient client)
    {
        _client = client;
    }

    protected override async Task<(IReadOnlyList<VariableResponse> Items, string? NextCursor)> FetchPageAsync(
        string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await _client.ListVariablesHandlerAsync(
            scope: null,
            groupId: null,
            projectId: null,
            limit: null,
            after: cursor,
            before: null,
            sortField: null,
            sortOrder: null,
            decrypt: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return (result.Data.ToList(), result.NextCursor);
    }

    internal override string GetDisplayText(VariableResponse item) => item.Name;

    internal override IReadOnlyList<KeyValuePair<string, string>> GetDetailPairs(VariableResponse item) =>
    [
        new("Id", item.Id.ToString()),
        new("Name", item.Name),
        new("Description", item.Description ?? "-"),
        new("Scope", item.Scope.ToString()),
        new("Group Id", item.GroupId?.ToString() ?? "-"),
        new("Project Id", item.ProjectId?.ToString() ?? "-"),
        new("Values", ScopedValueFormatter.Format(item.Values)),
        new("Is Sensitive", item.IsSensitive.ToString()),
        new("Version", item.Version.ToString(CultureInfo.InvariantCulture)),
        new("Created At", item.CreatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Created By", item.CreatedBy.ToString()),
        new("Updated At", item.UpdatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Updated By", item.UpdatedBy.ToString())
    ];

    protected override bool MatchesFilter(VariableResponse item, string filter) =>
        item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        (item.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
}