using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.Snapshots.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Snapshots;

internal sealed class ListSnapshotsHandler : IEndpointHandler
{
    private readonly ISnapshotStore _store;

    public ListSnapshotsHandler(ISnapshotStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(string.Empty, async (
                Guid projectId,
                [AsParameters] PaginationQuery query,
                [FromServices] ListSnapshotsHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(projectId, query, cancellationToken))
            .RequireAuthorization(Permissions.SnapshotsRead)
            .WithSummary("List snapshots")
            .WithDescription("Returns a paginated list of snapshots for the specified project.")
            .Produces<PaginatedResponse<SnapshotSummaryResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithName(nameof(ListSnapshotsHandler));
    }

    private async Task<IResult> HandleAsync(Guid projectId, PaginationQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var storeQuery = query.ToStoreQuery(projectId);
            var result = await _store.ListAsync(storeQuery, cancellationToken).ConfigureAwait(false);

            return TypedResults.Ok(new PaginatedResponse<SnapshotSummaryResponse>
            {
                Data = result.Items.Select(SnapshotSummaryResponse.From).ToList(),
                NextCursor = result.NextCursor,
                PreviousCursor = result.PreviousCursor,
                TotalCount = result.TotalCount,
            });
        }
        catch (ValidationException validationException)
        {
            return TypedResults.Problem(
                detail: validationException.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}