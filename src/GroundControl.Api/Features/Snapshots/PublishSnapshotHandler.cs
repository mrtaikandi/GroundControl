using GroundControl.Api.Features.Snapshots.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Observability;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Snapshots;

internal sealed class PublishSnapshotHandler : IEndpointHandler
{
    private readonly SnapshotPublisher _publisher;
    private readonly IProjectStore _projectStore;
    private readonly AuditRecorder _audit;

    public PublishSnapshotHandler(SnapshotPublisher publisher, IProjectStore projectStore, AuditRecorder audit)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _projectStore = projectStore ?? throw new ArgumentNullException(nameof(projectStore));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(string.Empty, async (
                Guid projectId,
                PublishSnapshotRequest request,
                [FromServices] PublishSnapshotHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(projectId, request, cancellationToken))
            .RequireAuthorization(Permissions.SnapshotsPublish)
            .WithName(nameof(PublishSnapshotHandler));
    }

    private async Task<IResult> HandleAsync(Guid projectId, PublishSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _publisher.PublishAsync(projectId, Guid.Empty, request.Description, cancellationToken).ConfigureAwait(false);

        return result.Result switch
        {
            Created<Snapshot> created => await OnPublished(created.Value!, projectId, cancellationToken),
            ProblemHttpResult problem => TypedResults.Problem(problem.ProblemDetails.Detail, statusCode: problem.StatusCode),
            NotFound => TypedResults.Problem(detail: $"Project '{projectId}' was not found.", statusCode: StatusCodes.Status404NotFound),
            _ => TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private async Task<IResult> OnPublished(Snapshot snapshot, Guid projectId, CancellationToken cancellationToken)
    {
        GroundControlMetrics.SnapshotsPublished.Add(1);

        var project = await _projectStore.GetByIdAsync(projectId, cancellationToken).ConfigureAwait(false);
        var metadata = new Dictionary<string, string> { ["ProjectId"] = projectId.ToString() };
        await _audit.RecordAsync("Snapshot", snapshot.Id, project?.GroupId, "Published", metadata: metadata, cancellationToken: cancellationToken).ConfigureAwait(false);

        return TypedResults.Created($"/api/projects/{projectId}/snapshots/{snapshot.Id}", SnapshotSummaryResponse.From(snapshot));
    }
}