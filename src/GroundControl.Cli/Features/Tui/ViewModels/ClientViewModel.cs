using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class ClientViewModel : ResourceViewModel<ClientResponse>
{
    private readonly IGroundControlClient _client;

    public ClientViewModel(IGroundControlClient client)
    {
        _client = client;
    }

    internal override string ResourceTypeName => "Client";

    public Guid? ProjectId { get; set; }

    protected override async Task<(IReadOnlyList<ClientResponse> Items, string? NextCursor)> FetchPageAsync(
        string? cursor,
        CancellationToken cancellationToken)
    {
        if (ProjectId is not { } projectId)
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

    internal override IReadOnlyList<DetailPair> GetDetailPairs(ClientResponse item) =>
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
        .. GetStandardMetadataPairs(new(item.Version, item.CreatedAt, item.CreatedBy, item.UpdatedAt, item.UpdatedBy))
    ];

    internal override string GetResourceName(ClientResponse item) => item.Name;

    internal override IReadOnlyList<FieldDefinition> GetFormFields() =>
    [
        new() { Label = "Name", Type = FieldType.Text, IsRequired = true },
        new() { Label = "Scopes", Type = FieldType.Text },
        new() { Label = "Expires At", Type = FieldType.Text }
    ];

    internal override IReadOnlyList<FieldDefinition> GetEditFormFields(ClientResponse item) =>
    [
        new() { Label = "Name", Type = FieldType.Text, IsRequired = true, DefaultValue = item.Name },
        new() { Label = "Is Active", Type = FieldType.Text, DefaultValue = item.IsActive.ToString() },
        new() { Label = "Expires At", Type = FieldType.Text, DefaultValue = item.ExpiresAt?.ToString("u", CultureInfo.InvariantCulture) ?? string.Empty }
    ];

    internal override async Task CreateAsync(Dictionary<string, string> fieldValues, CancellationToken cancellationToken = default)
    {
        if (ProjectId is not { } projectId)
        {
            throw new InvalidOperationException("No project selected.");
        }

        var request = new CreateClientRequest
        {
            Name = fieldValues["Name"],
            Scopes = ParseScopes(fieldValues.GetValueOrDefault("Scopes")),
            ExpiresAt = ParseDateTimeOffset(fieldValues.GetValueOrDefault("Expires At"))
        };

        await _client.CreateClientHandlerAsync(projectId, request, cancellationToken).ConfigureAwait(false);
    }

    internal override async Task UpdateAsync(ClientResponse item, Dictionary<string, string> fieldValues, CancellationToken cancellationToken = default)
    {
        GroundControlClient.SetIfMatch(item.Version);
        var request = new UpdateClientRequest
        {
            Name = fieldValues["Name"],
            IsActive = ParseBool(fieldValues.GetValueOrDefault("Is Active")) ?? false,
            ExpiresAt = ParseDateTimeOffset(fieldValues.GetValueOrDefault("Expires At"))
        };

        await _client.UpdateClientHandlerAsync(item.ProjectId, item.Id, request, cancellationToken).ConfigureAwait(false);
    }

    internal override async Task DeleteAsync(ClientResponse item, CancellationToken cancellationToken = default)
    {
        GroundControlClient.SetIfMatch(item.Version);
        await _client.DeleteClientHandlerAsync(item.ProjectId, item.Id, cancellationToken).ConfigureAwait(false);
    }

    protected override bool MatchesFilter(ClientResponse item, string filter) =>
        item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string>? ParseScopes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var result = new Dictionary<string, string>();
        foreach (var pair in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                result[parts[0].Trim()] = parts[1].Trim();
            }
        }

        return result.Count > 0 ? result : null;
    }

    private static DateTimeOffset? ParseDateTimeOffset(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) ? result : null;
}