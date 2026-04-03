using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.Pagination;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.Pagination.PaginatedRenderer;

namespace GroundControl.Cli.Features.Variables.List;

internal sealed class ListVariablesHandler : ICommandHandler
{
    private static readonly string[] Headers = ["Id", "Name", "Sensitive"];

    private static readonly Func<VariableResponse, string>[] ValueExtractors =
    [
        v => v.Id.ToString(),
        v => v.Name,
        v => v.IsSensitive.ToString()
    ];

    private readonly IShell _shell;
    private readonly ListVariablesOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public ListVariablesHandler(
        IShell shell,
        IOptions<ListVariablesOptions> options,
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
        await _shell.RenderPaginatedTableAsync<VariableResponse>(
            FetchPageAsync,
            Headers,
            ValueExtractors,
            _hostOptions.OutputFormat,
            cancellationToken);

        return 0;
    }

    private async Task<Page<VariableResponse>> FetchPageAsync(string? cursor, CancellationToken cancellationToken)
    {
        var result = await _client.ListVariablesHandlerAsync(
            scope: _options.Scope,
            groupId: _options.GroupId,
            projectId: _options.ProjectId,
            limit: null,
            after: cursor,
            before: null,
            sortField: null,
            sortOrder: null,
            decrypt: _options.Decrypt,
            cancellationToken: cancellationToken);

        return new Page<VariableResponse>((IReadOnlyList<VariableResponse>)result.Data, result.NextCursor);
    }
}