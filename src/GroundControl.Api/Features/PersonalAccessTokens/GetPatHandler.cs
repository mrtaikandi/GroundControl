using System.Security.Claims;
using GroundControl.Api.Features.PersonalAccessTokens.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.PersonalAccessTokens;

internal sealed class GetPatHandler : IEndpointHandler
{
    private readonly IPersonalAccessTokenStore _store;

    public GetPatHandler(IPersonalAccessTokenStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", async (
                Guid id,
                [FromServices] GetPatHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, httpContext, cancellationToken))
            .RequireAuthorization()
            .WithSummary("Get a personal access token")
            .WithDescription("Returns metadata for a personal access token owned by the current user.")
            .Produces<PatResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName(nameof(GetPatHandler));
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

        return TypedResults.Ok(PatResponse.From(pat));
    }
}