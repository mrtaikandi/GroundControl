using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class ProjectViewModel : ResourceViewModel<ProjectResponse>
{
    private readonly IGroundControlClient _client;

    public ProjectViewModel(IGroundControlClient client)
    {
        _client = client;
    }

    internal override string ResourceTypeName => "Project";

    protected override async Task<(IReadOnlyList<ProjectResponse> Items, string? NextCursor)> FetchPageAsync(
        string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await _client.ListProjectsHandlerAsync(
            groupId: null,
            search: null,
            limit: null,
            after: cursor,
            before: null,
            sortField: null,
            sortOrder: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return (result.Data.ToList(), result.NextCursor);
    }

    internal override string GetDisplayText(ProjectResponse item) => item.Name;

    internal override IReadOnlyList<KeyValuePair<string, string>> GetDetailPairs(ProjectResponse item) =>
    [
        new("Id", item.Id.ToString()),
        new("Name", item.Name),
        new("Description", item.Description ?? "-"),
        new("Group Id", item.GroupId?.ToString() ?? "-"),
        new("Template Ids", item.TemplateIds.Count > 0 ? string.Join(", ", item.TemplateIds) : "-"),
        new("Active Snapshot", item.ActiveSnapshotId?.ToString() ?? "-"),
        new("Version", item.Version.ToString(CultureInfo.InvariantCulture)),
        new("Created At", item.CreatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Created By", item.CreatedBy.ToString()),
        new("Updated At", item.UpdatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Updated By", item.UpdatedBy.ToString())
    ];

    internal override string GetResourceName(ProjectResponse item) => item.Name;

    internal override IReadOnlyList<FieldDefinition> GetFormFields() =>
    [
        new() { Label = "Name", Type = FieldType.Text, IsRequired = true },
        new() { Label = "Description", Type = FieldType.Text },
        new() { Label = "Group Id", Type = FieldType.Text },
        new() { Label = "Template Ids", Type = FieldType.Text }
    ];

    internal override IReadOnlyList<FieldDefinition> GetEditFormFields(ProjectResponse item) =>
    [
        new() { Label = "Name", Type = FieldType.Text, IsRequired = true, DefaultValue = item.Name },
        new() { Label = "Description", Type = FieldType.Text, DefaultValue = item.Description ?? string.Empty },
        new() { Label = "Group Id", Type = FieldType.Text, DefaultValue = item.GroupId?.ToString() ?? string.Empty },
        new() { Label = "Template Ids", Type = FieldType.Text, DefaultValue = item.TemplateIds.Count > 0 ? string.Join(", ", item.TemplateIds) : string.Empty }
    ];

    internal override async Task CreateAsync(Dictionary<string, string> fieldValues, CancellationToken cancellationToken = default)
    {
        var request = new CreateProjectRequest
        {
            Name = fieldValues["Name"],
            Description = NullIfEmpty(fieldValues.GetValueOrDefault("Description")),
            GroupId = ParseGuid(fieldValues.GetValueOrDefault("Group Id")),
            TemplateIds = ParseGuidList(fieldValues.GetValueOrDefault("Template Ids"))
        };

        await _client.CreateProjectHandlerAsync(request, cancellationToken).ConfigureAwait(false);
    }

    internal override async Task UpdateAsync(ProjectResponse item, Dictionary<string, string> fieldValues, CancellationToken cancellationToken = default)
    {
        GroundControlClient.SetIfMatch(item.Version);
        var request = new UpdateProjectRequest
        {
            Name = fieldValues["Name"],
            Description = NullIfEmpty(fieldValues.GetValueOrDefault("Description")),
            GroupId = ParseGuid(fieldValues.GetValueOrDefault("Group Id")),
            TemplateIds = ParseGuidList(fieldValues.GetValueOrDefault("Template Ids"))
        };

        await _client.UpdateProjectHandlerAsync(item.Id, request, cancellationToken).ConfigureAwait(false);
    }

    internal override async Task DeleteAsync(ProjectResponse item, CancellationToken cancellationToken = default)
    {
        GroundControlClient.SetIfMatch(item.Version);
        await _client.DeleteProjectHandlerAsync(item.Id, cancellationToken).ConfigureAwait(false);
    }

    protected override bool MatchesFilter(ProjectResponse item, string filter) =>
        item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        (item.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var guid) ? guid : null;

    private static List<Guid>? ParseGuidList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => Guid.TryParse(s, out _))
            .Select(Guid.Parse)
            .ToList();
    }
}