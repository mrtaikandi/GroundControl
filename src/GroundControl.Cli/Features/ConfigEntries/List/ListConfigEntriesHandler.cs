using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.Pagination;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.Pagination.PaginatedRenderer;

namespace GroundControl.Cli.Features.ConfigEntries.List;

internal sealed class ListConfigEntriesHandler : ICommandHandler
{
    private static readonly string[] Headers = ["Id", "Key", "OwnerId", "OwnerType", "ValueType", "Sensitive"];

    private static readonly Func<ConfigEntryResponse, string>[] ValueExtractors =
    [
        e => e.Id.ToString()[..8],
        e => e.Key,
        e => e.OwnerId.ToString()[..8],
        e => e.OwnerType.ToString(),
        e => e.ValueType,
        e => e.IsSensitive.ToString()
    ];

    private readonly IShell _shell;
    private readonly ListConfigEntriesOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public ListConfigEntriesHandler(
        IShell shell,
        IOptions<ListConfigEntriesOptions> options,
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
        await _shell.RenderPaginatedTableAsync<ConfigEntryResponse>(
            FetchPageAsync,
            Headers,
            ValueExtractors,
            _hostOptions.OutputFormat,
            cancellationToken);

        return 0;
    }

    private async Task<Page<ConfigEntryResponse>> FetchPageAsync(string? cursor, CancellationToken cancellationToken)
    {
        var result = await _client.ListConfigEntriesHandlerAsync(
            ownerId: _options.OwnerId,
            ownerType: _options.OwnerType,
            keyPrefix: _options.KeyPrefix,
            limit: null,
            after: cursor,
            before: null,
            sortField: null,
            sortOrder: null,
            decrypt: _options.Decrypt,
            cancellationToken: cancellationToken);

        return new Page<ConfigEntryResponse>((IReadOnlyList<ConfigEntryResponse>)result.Data, result.NextCursor);
    }
}