using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class VariableViewModel : ResourceViewModel<VariableResponse>
{
    private readonly IGroundControlClient _client;

    public VariableViewModel(IGroundControlClient client)
    {
        _client = client;
    }

    internal override string ResourceTypeName => "Variable";

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

    internal override string GetResourceName(VariableResponse item) => item.Name;

    internal override IReadOnlyList<FieldDefinition> GetFormFields() =>
    [
        new() { Label = "Name", Type = FieldType.Text, IsRequired = true },
        new() { Label = "Scope", Type = FieldType.Text, IsRequired = true },
        new() { Label = "Description", Type = FieldType.Text },
        new() { Label = "Default Value", Type = FieldType.Text },
        new() { Label = "Is Sensitive", Type = FieldType.Text },
        new() { Label = "Group Id", Type = FieldType.Text },
        new() { Label = "Project Id", Type = FieldType.Text }
    ];

    internal override IReadOnlyList<FieldDefinition> GetEditFormFields(VariableResponse item) =>
    [
        new() { Label = "Description", Type = FieldType.Text, DefaultValue = item.Description ?? string.Empty },
        new() { Label = "Default Value", Type = FieldType.Text, DefaultValue = ScopedValueFormatter.Format(item.Values) },
        new() { Label = "Is Sensitive", Type = FieldType.Text, DefaultValue = item.IsSensitive.ToString() }
    ];

    internal override async Task CreateAsync(Dictionary<string, string> fieldValues, CancellationToken cancellationToken = default)
    {
        var request = new CreateVariableRequest
        {
            Name = fieldValues["Name"],
            Scope = Enum.TryParse<VariableScope>(fieldValues["Scope"], true, out var scope) ? scope : VariableScope.Global,
            Description = NullIfEmpty(fieldValues.GetValueOrDefault("Description")),
            IsSensitive = bool.TryParse(fieldValues.GetValueOrDefault("Is Sensitive"), out var isSensitive) ? isSensitive : null,
            GroupId = Guid.TryParse(fieldValues.GetValueOrDefault("Group Id"), out var groupId) ? groupId : null,
            ProjectId = Guid.TryParse(fieldValues.GetValueOrDefault("Project Id"), out var projectId) ? projectId : null,
            Values = [new ScopedValueRequest { Value = fieldValues.GetValueOrDefault("Default Value") ?? string.Empty }]
        };

        await _client.CreateVariableHandlerAsync(request, cancellationToken).ConfigureAwait(false);
    }

    internal override async Task UpdateAsync(VariableResponse item, Dictionary<string, string> fieldValues, CancellationToken cancellationToken = default)
    {
        GroundControlClient.SetIfMatch(item.Version);
        var request = new UpdateVariableRequest
        {
            Description = NullIfEmpty(fieldValues.GetValueOrDefault("Description")),
            IsSensitive = bool.TryParse(fieldValues.GetValueOrDefault("Is Sensitive"), out var isSensitive) ? isSensitive : null,
            Values = [new ScopedValueRequest { Value = fieldValues.GetValueOrDefault("Default Value") ?? string.Empty }]
        };

        await _client.UpdateVariableHandlerAsync(item.Id, request, cancellationToken).ConfigureAwait(false);
    }

    internal override async Task DeleteAsync(VariableResponse item, CancellationToken cancellationToken = default)
    {
        GroundControlClient.SetIfMatch(item.Version);
        await _client.DeleteVariableHandlerAsync(item.Id, cancellationToken).ConfigureAwait(false);
    }

    protected override bool MatchesFilter(VariableResponse item, string filter) =>
        item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        (item.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
}