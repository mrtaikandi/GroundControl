using GroundControl.Api.Shared;

namespace GroundControl.Api.Features.Authentication;

internal sealed class ExternalCallbackHandler : IEndpointHandler
{
    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/callback", (
                HttpContext httpContext,
                string? returnUrl = null) => HandleAsync(httpContext, returnUrl))
            .RequireAuthorization()
            .WithName(nameof(ExternalCallbackHandler));
    }

    private static IResult HandleAsync(HttpContext httpContext, string? returnUrl)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return TypedResults.Problem(
                detail: "Authentication failed.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var redirectTo = returnUrl ?? "/";

        return TypedResults.Redirect(redirectTo);
    }
}