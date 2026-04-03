using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.Pagination;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.Pagination.PaginatedRenderer;

namespace GroundControl.Cli.Features.Scopes.List;

internal sealed class ListScopesHandler : ICommandHandler
{
    private static readonly string[] Headers = ["Id", "Dimension", "Values"];

    private static readonly Func<ScopeResponse, string>[] ValueExtractors =
    [
        s => s.Id.ToString(),
        s => s.Dimension,
        s => s.AllowedValues.Count.ToString(CultureInfo.InvariantCulture)
    ];

    private readonly IShell _shell;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public ListScopesHandler(
        IShell shell,
        IOptions<CliHostOptions> hostOptions,
        IGroundControlClient client)
    {
        _shell = shell;
        _hostOptions = hostOptions.Value;
        _client = client;
    }

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        await _shell.RenderPaginatedTableAsync<ScopeResponse>(
            FetchPageAsync,
            Headers,
            ValueExtractors,
            _hostOptions.OutputFormat,
            cancellationToken);

        return 0;
    }

    private async Task<Page<ScopeResponse>> FetchPageAsync(string? cursor, CancellationToken cancellationToken)
    {
        var result = await _client.ListScopesHandlerAsync(
            limit: null,
            after: cursor,
            before: null,
            sortField: null,
            sortOrder: null,
            cancellationToken: cancellationToken);

        return new Page<ScopeResponse>((IReadOnlyList<ScopeResponse>)result.Data, result.NextCursor);
    }
}