using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.Pagination;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.Pagination.PaginatedRenderer;

namespace GroundControl.Cli.Features.Clients.List;

internal sealed class ListClientsHandler : ICommandHandler
{
    private static readonly string[] Headers = ["Id", "Name", "IsActive", "ScopeCount", "CreatedAt"];

    private static readonly Func<ClientResponse, string>[] ValueExtractors =
    [
        c => c.Id.ToString(),
        c => c.Name,
        c => c.IsActive.ToString(),
        c => c.Scopes.Count.ToString(CultureInfo.InvariantCulture),
        c => c.CreatedAt.ToString("O")
    ];

    private readonly IShell _shell;
    private readonly ListClientsOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public ListClientsHandler(
        IShell shell,
        IOptions<ListClientsOptions> options,
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
        await _shell.RenderPaginatedTableAsync<ClientResponse>(
            FetchPageAsync,
            Headers,
            ValueExtractors,
            _hostOptions.OutputFormat,
            cancellationToken);

        return 0;
    }

    private async Task<Page<ClientResponse>> FetchPageAsync(string? cursor, CancellationToken cancellationToken)
    {
        var result = await _client.ListClientsHandlerAsync(
            _options.ProjectId,
            limit: null,
            after: cursor,
            before: null,
            sortField: null,
            sortOrder: null,
            cancellationToken: cancellationToken);

        return new Page<ClientResponse>((IReadOnlyList<ClientResponse>)result.Data, result.NextCursor);
    }
}