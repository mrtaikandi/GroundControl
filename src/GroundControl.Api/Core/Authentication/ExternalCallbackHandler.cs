
namespace GroundControl.Api.Core.Authentication;

internal sealed class ExternalCallbackHandler : IEndpointHandler
{
    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/callback", (
                HttpContext httpContext,
                string? returnUrl = null) => HandleAsync(httpContext, returnUrl))
            .RequireAuthorization()
            .WithSummary("External login callback")
            .WithDescription("Handles the callback from the external authentication provider and redirects to the application.")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
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