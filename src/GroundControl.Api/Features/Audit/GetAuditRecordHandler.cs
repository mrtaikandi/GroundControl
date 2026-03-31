using GroundControl.Api.Features.Audit.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Audit;

internal sealed class GetAuditRecordHandler : IEndpointHandler
{
    private readonly IAuditStore _auditStore;
    private readonly IUserStore _userStore;

    public GetAuditRecordHandler(IAuditStore auditStore, IUserStore userStore)
    {
        _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                [FromServices] GetAuditRecordHandler handler,
                CancellationToken cancellationToken = default) =>
                await handler.HandleAsync(id, httpContext, cancellationToken))
            .WithName(nameof(GetAuditRecordHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var record = await _auditStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return TypedResults.Problem(
                detail: $"Audit record '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var accessibleGroupIds = await AccessibleGroupResolver.GetAccessibleGroupIdsAsync(
            _userStore, httpContext.User, cancellationToken).ConfigureAwait(false);

        // accessibleGroupIds == null means system-wide access (all groups).
        // Otherwise, the record's GroupId must be in the list. Records with
        // GroupId == null (system-level actions) are only visible to system-wide users.
        if (accessibleGroupIds is not null && !accessibleGroupIds.Contains(record.GroupId))
        {
            return TypedResults.Problem(
                detail: $"Audit record '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(AuditRecordResponse.From(record));
    }
}