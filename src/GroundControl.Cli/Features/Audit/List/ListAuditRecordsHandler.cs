using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.Pagination;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.Pagination.PaginatedRenderer;

namespace GroundControl.Cli.Features.Audit.List;

internal sealed class ListAuditRecordsHandler : ICommandHandler
{
    private static readonly string[] Headers = ["Id", "EntityType", "Action", "PerformedBy", "PerformedAt"];

    private static readonly Func<AuditRecordResponse, string>[] ValueExtractors =
    [
        r => r.Id.ToString(),
        r => r.EntityType,
        r => r.Action,
        r => r.PerformedBy.ToString(),
        r => r.PerformedAt.ToString("O")
    ];

    private readonly IShell _shell;
    private readonly ListAuditRecordsOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public ListAuditRecordsHandler(
        IShell shell,
        IOptions<ListAuditRecordsOptions> options,
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
        await _shell.RenderPaginatedTableAsync<AuditRecordResponse>(
            FetchPageAsync,
            Headers,
            ValueExtractors,
            _hostOptions.OutputFormat,
            cancellationToken);

        return 0;
    }

    private async Task<Page<AuditRecordResponse>> FetchPageAsync(string? cursor, CancellationToken cancellationToken)
    {
        var result = await _client.ListAuditRecordsHandlerAsync(
            entityType: _options.EntityType,
            entityId: _options.EntityId,
            performedBy: null,
            from: null,
            to: null,
            after: cursor,
            before: null,
            limit: null,
            cancellationToken: cancellationToken);

        return new Page<AuditRecordResponse>((IReadOnlyList<AuditRecordResponse>)result.Data, result.NextCursor);
    }
}