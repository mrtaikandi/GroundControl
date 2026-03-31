using System.Security.Claims;
using GroundControl.Api.Shared.Audit;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.PersonalAccessTokens;

internal sealed class RevokePatHandler : IEndpointHandler
{
    private readonly IPersonalAccessTokenStore _store;
    private readonly AuditRecorder _audit;

    public RevokePatHandler(IPersonalAccessTokenStore store, AuditRecorder audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/{id:guid}", async (
                Guid id,
                [FromServices] RevokePatHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, httpContext, cancellationToken))
            .RequireAuthorization()
            .WithName(nameof(RevokePatHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return TypedResults.Problem(
                detail: "Unable to determine user identity.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var pat = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (pat is null || pat.UserId != userId)
        {
            return TypedResults.Problem(
                detail: $"Personal access token '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        await _store.RevokeAsync(id, cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync("PersonalAccessToken", id, null, "Revoked", cancellationToken: cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}