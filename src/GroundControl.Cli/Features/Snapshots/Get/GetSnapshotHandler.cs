using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Snapshots.Get;

internal sealed class GetSnapshotHandler : ICommandHandler
{
    private const string MaskedValue = "********";

    private readonly IShell _shell;
    private readonly GetSnapshotOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public GetSnapshotHandler(
        IShell shell,
        IOptions<GetSnapshotOptions> options,
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
            var snapshot = await _client.GetSnapshotHandlerAsync(
                _options.ProjectId,
                _options.Id,
                _options.Decrypt,
                cancellationToken);

            _shell.RenderDetail(BuildDetail(snapshot), _hostOptions.OutputFormat);

            if (snapshot.Entries.Count > 0 && _hostOptions.OutputFormat != OutputFormat.Json)
            {
                _shell.DisplayEmptyLine();
                RenderEntries(snapshot);
            }

            return 0;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }
    }

    private static List<(string Key, string Value)> BuildDetail(SnapshotResponse snapshot) =>
    [
        ("Id", snapshot.Id.ToString()),
        ("Project Id", snapshot.ProjectId.ToString()),
        ("Version", snapshot.SnapshotVersion.ToString(CultureInfo.InvariantCulture)),
        ("Entry Count", snapshot.Entries.Count.ToString(CultureInfo.InvariantCulture)),
        ("Published At", snapshot.PublishedAt.ToString("O")),
        ("Published By", snapshot.PublishedBy.ToString()),
        ("Description", snapshot.Description ?? string.Empty)
    ];

    private void RenderEntries(SnapshotResponse snapshot)
    {
        var headers = new[] { "Key", "Type", "Sensitive", "Scope", "Value" };
        var rows = new List<string[]>();

        foreach (var entry in snapshot.Entries)
        {
            if (entry.Values.Count == 0)
            {
                rows.Add([entry.Key, entry.ValueType, entry.IsSensitive.ToString(), string.Empty, string.Empty]);
                continue;
            }

            foreach (var sv in entry.Values)
            {
                var scopeLabel = sv.Scopes is { Count: > 0 }
                    ? string.Join(", ", sv.Scopes.Select(s => $"{s.Key}:{s.Value}"))
                    : "default";
                var displayValue = entry.IsSensitive && _options.Decrypt != true ? MaskedValue : sv.Value;
                rows.Add([entry.Key, entry.ValueType, entry.IsSensitive.ToString(), scopeLabel, displayValue]);
            }
        }

        _shell.RenderTable(
            (IReadOnlyList<string[]>)rows,
            headers,
            [r => r[0], r => r[1], r => r[2], r => r[3], r => r[4]],
            _hostOptions.OutputFormat);
    }
}