using System.Security.Claims;
using GroundControl.Api.Core.Authentication.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Core.Authentication;

internal sealed class GetCurrentUserHandler : IEndpointHandler
{
    private readonly IUserStore _userStore;

    public GetCurrentUserHandler(IUserStore userStore)
    {
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/me", async (
                [FromServices] GetCurrentUserHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(httpContext, cancellationToken))
            .RequireAuthorization()
            .WithSummary("Get current user")
            .WithDescription("Returns the profile of the currently authenticated user.")
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName(nameof(GetCurrentUserHandler));
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

        var user = await _userStore.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return TypedResults.Problem(
                detail: "User not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(UserResponse.From(user));
    }
}