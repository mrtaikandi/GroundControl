using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.Pagination;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.Pagination.PaginatedRenderer;

namespace GroundControl.Cli.Features.Groups.List;

internal sealed class ListGroupsHandler : ICommandHandler
{
    private static readonly string[] Headers = ["Id", "Name", "Description"];

    private static readonly Func<GroupResponse, string>[] ValueExtractors =
    [
        g => g.Id.ToString(),
        g => g.Name,
        g => g.Description ?? string.Empty
    ];

    private readonly IShell _shell;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public ListGroupsHandler(
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
        await _shell.RenderPaginatedTableAsync<GroupResponse>(
            FetchPageAsync,
            Headers,
            ValueExtractors,
            _hostOptions.OutputFormat,
            cancellationToken);

        return 0;
    }

    private async Task<Page<GroupResponse>> FetchPageAsync(string? cursor, CancellationToken cancellationToken)
    {
        var result = await _client.ListGroupsHandlerAsync(
            limit: null,
            after: cursor,
            before: null,
            sortField: null,
            sortOrder: null,
            cancellationToken: cancellationToken);

        return new Page<GroupResponse>((IReadOnlyList<GroupResponse>)result.Data, result.NextCursor);
    }
}