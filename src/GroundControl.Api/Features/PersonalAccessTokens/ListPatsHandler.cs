using System.Security.Claims;
using GroundControl.Api.Features.PersonalAccessTokens.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.PersonalAccessTokens;

internal sealed class ListPatsHandler : IEndpointHandler
{
    private readonly IPersonalAccessTokenStore _store;

    public ListPatsHandler(IPersonalAccessTokenStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(string.Empty, async (
                [FromServices] ListPatsHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(httpContext, cancellationToken))
            .RequireAuthorization()
            .WithName(nameof(ListPatsHandler));
    }

    private async Task<IResult> HandleAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return TypedResults.Problem(
                detail: "Unable to determine user identity.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var tokens = await _store.ListByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(tokens.Select(PatResponse.From).ToList());
    }
}