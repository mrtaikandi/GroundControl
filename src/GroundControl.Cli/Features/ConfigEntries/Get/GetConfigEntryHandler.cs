using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.ConfigEntries.Get;

internal sealed class GetConfigEntryHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly GetConfigEntryOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public GetConfigEntryHandler(
        IShell shell,
        IOptions<GetConfigEntryOptions> options,
        IOptions<CliHostOptions> hostOptions,
        IGroundControlClient client)
    {
        _shell = shell;
        _options = options.Value;
        _hostOptions = hostOptions.Value;
        _client = client;
    }

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        try
        {
            var entry = await _client.GetConfigEntryHandlerAsync(_options.Id, _options.Decrypt, cancellationToken);
            _shell.RenderDetail(BuildDetail(entry), _hostOptions.OutputFormat);
            return 0;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }
    }

    private static List<(string Key, string Value)> BuildDetail(ConfigEntryResponse entry)
    {
        var details = new List<(string Key, string Value)>
        {
            ("Id", entry.Id.ToString()),
            ("Key", entry.Key),
            ("Owner Id", entry.OwnerId.ToString()),
            ("Owner Type", entry.OwnerType.ToString()),
            ("Value Type", entry.ValueType),
            ("Sensitive", entry.IsSensitive.ToString())
        };

        foreach (var sv in entry.Values)
        {
            var scopeLabel = sv.Scopes is { Count: > 0 }
                ? string.Join(", ", sv.Scopes.Select(s => $"{s.Key}:{s.Value}"))
                : "default";
            details.Add(($"Value [{scopeLabel}]", sv.Value));
        }

        details.Add(("Description", entry.Description ?? string.Empty));
        details.Add(("Version", entry.Version.ToString(CultureInfo.InvariantCulture)));
        details.Add(("Created At", entry.CreatedAt.ToString("O")));
        details.Add(("Updated At", entry.UpdatedAt.ToString("O")));

        return details;
    }
}