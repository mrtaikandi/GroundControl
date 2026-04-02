using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Core.ChangeNotification;
using GroundControl.Api.Shared.Observability;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Snapshots;

internal sealed class ActivateSnapshotHandler : IEndpointHandler
{
    private readonly IProjectStore _projectStore;
    private readonly ISnapshotStore _snapshotStore;
    private readonly IChangeNotifier _changeNotifier;
    private readonly AuditRecorder _audit;

    public ActivateSnapshotHandler(IProjectStore projectStore, ISnapshotStore snapshotStore, IChangeNotifier changeNotifier, AuditRecorder audit)
    {
        _projectStore = projectStore ?? throw new ArgumentNullException(nameof(projectStore));
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _changeNotifier = changeNotifier ?? throw new ArgumentNullException(nameof(changeNotifier));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/{id:guid}/activate", async (
                Guid projectId,
                Guid id,
                [FromServices] ActivateSnapshotHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(projectId, id, cancellationToken))
            .RequireAuthorization(Permissions.SnapshotsPublish)
            .WithSummary("Activate a snapshot")
            .WithDescription("Sets the specified snapshot as the active configuration for its project, notifying connected clients.")
            .Produces<ProjectResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(ActivateSnapshotHandler));
    }

    private async Task<IResult> HandleAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _projectStore.GetByIdAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return TypedResults.Problem(
                detail: $"Project '{projectId}' was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var snapshot = await _snapshotStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (snapshot is null || snapshot.ProjectId != projectId)
        {
            return TypedResults.Problem(
                detail: $"Snapshot '{id}' was not found for project '{projectId}'.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (project.ActiveSnapshotId == id)
        {
            return TypedResults.Problem(
                detail: $"Snapshot '{id}' is already the active snapshot.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var oldSnapshotId = project.ActiveSnapshotId;

        var activated = await _projectStore.ActivateSnapshotAsync(projectId, id, project.Version, cancellationToken).ConfigureAwait(false);
        if (!activated)
        {
            return TypedResults.Problem(
                detail: "Version conflict.",
                statusCode: StatusCodes.Status409Conflict);
        }

        await _changeNotifier.NotifyAsync(projectId, id, cancellationToken).ConfigureAwait(false);
        GroundControlMetrics.SnapshotsActivated.Add(1);

        List<FieldChange> changes = [.. AuditRecorder.CompareFields("ActiveSnapshotId", oldSnapshotId, id)];
        await _audit.RecordAsync("Snapshot", id, project.GroupId, "Activated", changes, cancellationToken: cancellationToken).ConfigureAwait(false);

        var updatedProject = await _projectStore.GetByIdAsync(projectId, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(ProjectResponse.From(updatedProject!));
    }
}