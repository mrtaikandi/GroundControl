using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace GroundControl.Api.Core.Authentication;

internal sealed class ExternalLoginHandler : IEndpointHandler
{
    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/login/external", async (
                HttpContext httpContext,
                string? returnUrl = null,
                CancellationToken cancellationToken = default) => await HandleAsync(httpContext, returnUrl))
            .AllowAnonymous()
            .WithSummary("Initiate external login")
            .WithDescription("Redirects to the configured OpenID Connect provider for authentication.")
            .Produces(StatusCodes.Status302Found)
            .WithName(nameof(ExternalLoginHandler));
    }

    private static Task<IResult> HandleAsync(HttpContext httpContext, string? returnUrl)
    {
        var callbackUrl = string.IsNullOrEmpty(returnUrl)
            ? "/auth/callback"
            : $"/auth/callback?returnUrl={Uri.EscapeDataString(returnUrl)}";

        var properties = new AuthenticationProperties { RedirectUri = callbackUrl };

        return Task.FromResult<IResult>(TypedResults.Challenge(properties, [OpenIdConnectDefaults.AuthenticationScheme]));
    }
}