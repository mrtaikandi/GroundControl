using System.Security.Claims;
using GroundControl.Api.Features.Users.Contracts;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Users;

internal sealed class GetUserHandler : IEndpointHandler
{
    private readonly IUserStore _userStore;
    private readonly IAuthorizationService _authorizationService;

    public GetUserHandler(IUserStore userStore, IAuthorizationService authorizationService)
    {
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                [FromServices] GetUserHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, httpContext, cancellationToken))
            .RequireAuthorization()
            .WithName(nameof(GetUserHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!IsSelf(httpContext, id))
        {
            var authResult = await _authorizationService.AuthorizeAsync(httpContext.User, Permissions.UsersRead).ConfigureAwait(false);
            if (!authResult.Succeeded)
            {
                return TypedResults.Problem(detail: "Forbidden.", statusCode: StatusCodes.Status403Forbidden);
            }
        }

        var user = await _userStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return TypedResults.Problem(detail: $"User '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(user.Version);
        return TypedResults.Ok(UserResponse.From(user));
    }

    private static bool IsSelf(HttpContext httpContext, Guid targetId)
    {
        var sub = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var callerId) && callerId == targetId;
    }
}