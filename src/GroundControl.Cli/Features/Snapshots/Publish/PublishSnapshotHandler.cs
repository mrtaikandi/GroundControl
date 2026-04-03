using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Snapshots.Publish;

internal sealed class PublishSnapshotHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly PublishSnapshotOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public PublishSnapshotHandler(
        IShell shell,
        IOptions<PublishSnapshotOptions> options,
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
        var projectId = _options.ProjectId;

        if (projectId is null && _hostOptions.NoInteractive)
        {
            _shell.DisplayError("Missing required option: --project-id. Provide it explicitly when using --no-interactive.");
            return 1;
        }

        if (projectId is null)
        {
            projectId = await PromptForProjectSelectionAsync(cancellationToken);

            if (projectId is null)
            {
                _shell.DisplayError("No projects available.");
                return 1;
            }
        }

        try
        {
            var request = new PublishSnapshotRequest
            {
                Description = _options.Description
            };

            var snapshot = await _shell.ShowStatusAsync(
                "Publishing snapshot...",
                () => _client.PublishSnapshotHandlerAsync(projectId.Value, request, cancellationToken));

            _shell.DisplaySuccess(
                $"Snapshot published (version: {snapshot.SnapshotVersion}, entries: {snapshot.EntryCount}).");

            return 0;
        }
        catch (GroundControlApiClientException<HttpValidationProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }
    }

    private async Task<Guid?> PromptForProjectSelectionAsync(CancellationToken cancellationToken)
    {
        var projects = new List<ProjectResponse>();
        string? cursor = null;

        do
        {
            var page = await _client.ListProjectsHandlerAsync(
                groupId: null,
                search: null,
                limit: null,
                after: cursor,
                before: null,
                sortField: null,
                sortOrder: null,
                cancellationToken: cancellationToken);

            projects.AddRange(page.Data);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        if (projects.Count == 0)
        {
            return null;
        }

        var selected = await _shell.PromptForSelectionAsync(
            "Select project:",
            (IReadOnlyCollection<ProjectResponse>)projects,
            p => $"{p.Name} ({p.Id})",
            enableSearch: true,
            cancellationToken: cancellationToken);

        return selected.Id;
    }
}