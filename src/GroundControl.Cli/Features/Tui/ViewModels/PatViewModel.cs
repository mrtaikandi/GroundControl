using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class PatViewModel : ResourceViewModel<PatResponse>
{
    private readonly IGroundControlClient _client;

    public PatViewModel(IGroundControlClient client)
    {
        _client = client;
    }

    internal override string ResourceTypeName => "Token";

    protected override async Task<(IReadOnlyList<PatResponse> Items, string? NextCursor)> FetchPageAsync(
        string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await _client.ListPatsHandlerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        return ([.. result], null);
    }

    internal override string GetDisplayText(PatResponse item) =>
        $"{item.Name} ({item.TokenPrefix}...)";

    internal override IReadOnlyList<DetailPair> GetDetailPairs(PatResponse item) =>
    [
        new("Id", item.Id.ToString()),
        new("Name", item.Name),
        new("Token Prefix", item.TokenPrefix),
        new("Permissions", item.Permissions is { Count: > 0 } ? string.Join(", ", item.Permissions) : "-"),
        new("Is Revoked", (item.IsRevoked ?? false).ToString()),
        new("Expires At", item.ExpiresAt?.ToString("u", CultureInfo.InvariantCulture) ?? "-"),
        new("Last Used At", item.LastUsedAt?.ToString("u", CultureInfo.InvariantCulture) ?? "-"),
        new("Created At", item.CreatedAt.ToString("u", CultureInfo.InvariantCulture))
    ];

    internal override string GetResourceName(PatResponse item) => item.Name;

    internal override IReadOnlyList<FieldDefinition> GetFormFields() =>
    [
        new() { Label = "Name", Type = FieldType.Text, IsRequired = true },
        new() { Label = "Expires In Days", Type = FieldType.Text },
        new() { Label = "Permissions", Type = FieldType.Text }
    ];

    internal override IReadOnlyList<FieldDefinition> GetEditFormFields(PatResponse item) =>
        throw new NotSupportedException("Tokens cannot be edited.");

    internal override async Task CreateAsync(Dictionary<string, string> fieldValues, CancellationToken cancellationToken = default)
    {
        var request = new CreatePatRequest
        {
            Name = fieldValues["Name"],
            ExpiresInDays = int.TryParse(fieldValues.GetValueOrDefault("Expires In Days"), out var days) ? days : null,
            Permissions = ParseCommaSeparated(fieldValues.GetValueOrDefault("Permissions")) is { Count: > 0 } perms ? perms : null
        };

        await _client.CreatePatHandlerAsync(request, cancellationToken).ConfigureAwait(false);
    }

    internal override Task UpdateAsync(PatResponse item, Dictionary<string, string> fieldValues, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Tokens cannot be edited.");

    internal override async Task DeleteAsync(PatResponse item, CancellationToken cancellationToken = default)
    {
        await _client.RevokePatHandlerAsync(item.Id, cancellationToken).ConfigureAwait(false);
    }

    protected override bool MatchesFilter(PatResponse item, string filter) =>
        item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        item.TokenPrefix.Contains(filter, StringComparison.OrdinalIgnoreCase);

}