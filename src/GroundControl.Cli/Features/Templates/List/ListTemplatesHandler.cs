using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.Pagination;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.Pagination.PaginatedRenderer;

namespace GroundControl.Cli.Features.Templates.List;

internal sealed class ListTemplatesHandler : ICommandHandler
{
    private static readonly string[] Headers = ["Id", "Name", "GroupId"];

    private static readonly Func<TemplateResponse, string>[] ValueExtractors =
    [
        t => t.Id.ToString(),
        t => t.Name,
        t => t.GroupId?.ToString() ?? string.Empty
    ];

    private readonly IShell _shell;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public ListTemplatesHandler(
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
        await _shell.RenderPaginatedTableAsync<TemplateResponse>(
            FetchPageAsync,
            Headers,
            ValueExtractors,
            _hostOptions.OutputFormat,
            cancellationToken);

        return 0;
    }

    private async Task<Page<TemplateResponse>> FetchPageAsync(string? cursor, CancellationToken cancellationToken)
    {
        var result = await _client.ListTemplatesHandlerAsync(
            groupId: null,
            globalOnly: null,
            limit: null,
            after: cursor,
            before: null,
            sortField: null,
            sortOrder: null,
            cancellationToken: cancellationToken);

        return new Page<TemplateResponse>((IReadOnlyList<TemplateResponse>)result.Data, result.NextCursor);
    }
}