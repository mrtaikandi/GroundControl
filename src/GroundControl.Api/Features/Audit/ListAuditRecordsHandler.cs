using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.Audit.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Audit;

internal sealed class ListAuditRecordsHandler : IEndpointHandler
{
    private const int DefaultLimit = 50;

    private readonly IAuditStore _auditStore;
    private readonly IUserStore _userStore;

    public ListAuditRecordsHandler(IAuditStore auditStore, IUserStore userStore)
    {
        _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(string.Empty, async (
                string? entityType,
                Guid? entityId,
                Guid? performedBy,
                DateTimeOffset? from,
                DateTimeOffset? to,
                string? after,
                string? before,
                [Range(1, 100)] int? limit,
                HttpContext httpContext,
                [FromServices] ListAuditRecordsHandler handler,
                CancellationToken cancellationToken = default) =>
                await handler.HandleAsync(entityType, entityId, performedBy, from, to, after, before, limit, httpContext, cancellationToken))
            .WithSummary("List audit records")
            .WithDescription("Returns a paginated list of audit records with optional filters for entity type, entity ID, performer, and date range.")
            .Produces<PaginatedResponse<AuditRecordResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithName(nameof(ListAuditRecordsHandler));
    }

    private async Task<IResult> HandleAsync(
        string? entityType,
        Guid? entityId,
        Guid? performedBy,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? after,
        string? before,
        int? limit,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var accessibleGroupIds = await AccessibleGroupResolver.GetAccessibleGroupIdsAsync(
                _userStore, httpContext.User, cancellationToken).ConfigureAwait(false);

            var query = new AuditListQuery
            {
                EntityType = entityType,
                EntityId = entityId,
                PerformedBy = performedBy,
                From = from,
                To = to,
                After = after,
                Before = before,
                Limit = limit ?? DefaultLimit,
                SortField = "performedAt",
                SortOrder = "desc",
                AccessibleGroupIds = accessibleGroupIds,
            };

            var result = await _auditStore.ListAsync(query, cancellationToken).ConfigureAwait(false);

            return TypedResults.Ok(new PaginatedResponse<AuditRecordResponse>
            {
                Data = result.Items.Select(AuditRecordResponse.From).ToList(),
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