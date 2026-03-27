using System.Security.Claims;
using GroundControl.Api.Features.Audit.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
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
            .RequireAuthorization(Permissions.AuditRead)
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

        var accessibleGroupIds = await GetAccessibleGroupIdsAsync(httpContext.User, cancellationToken).ConfigureAwait(false);
        if (accessibleGroupIds is not null && !accessibleGroupIds.Contains(record.GroupId))
        {
            return TypedResults.Problem(
                detail: $"Audit record '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(AuditRecordResponse.From(record));
    }

    private async Task<IReadOnlyList<Guid?>?> GetAccessibleGroupIdsAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (sub is null || !Guid.TryParse(sub, out var userId) || userId == Guid.Empty)
        {
            return null;
        }

        var user = await _userStore.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return [];
        }

        if (user.Grants.Any(g => g.Resource is null))
        {
            return null;
        }

        return user.Grants.Select(g => g.Resource).Distinct().ToList();
    }
}