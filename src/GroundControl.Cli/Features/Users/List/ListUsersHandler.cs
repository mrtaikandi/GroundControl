using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.Pagination;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.Pagination.PaginatedRenderer;

namespace GroundControl.Cli.Features.Users.List;

internal sealed class ListUsersHandler : ICommandHandler
{
    private static readonly string[] Headers = ["Id", "Username", "Email"];

    private static readonly Func<UserResponse, string>[] ValueExtractors =
    [
        u => u.Id.ToString(),
        u => u.Username,
        u => u.Email
    ];

    private readonly IShell _shell;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public ListUsersHandler(
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
        await _shell.RenderPaginatedTableAsync<UserResponse>(
            FetchPageAsync,
            Headers,
            ValueExtractors,
            _hostOptions.OutputFormat,
            cancellationToken);

        return 0;
    }

    private async Task<Page<UserResponse>> FetchPageAsync(string? cursor, CancellationToken cancellationToken)
    {
        var result = await _client.ListUsersHandlerAsync(
            limit: null,
            after: cursor,
            before: null,
            sortField: null,
            sortOrder: null,
            cancellationToken: cancellationToken);

        return new Page<UserResponse>((IReadOnlyList<UserResponse>)result.Data, result.NextCursor);
    }
}