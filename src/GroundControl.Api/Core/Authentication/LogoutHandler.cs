using AspNetCore.Identity.MongoDbCore.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Core.Authentication;

internal sealed class LogoutHandler : IEndpointHandler
{
    private readonly SignInManager<MongoIdentityUser<Guid>> _signInManager;

    public LogoutHandler(SignInManager<MongoIdentityUser<Guid>> signInManager)
    {
        _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/logout", async (
                [FromServices] LogoutHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync())
            .RequireAuthorization()
            .WithSummary("Log out")
            .WithDescription("Signs the current user out and clears the session cookie.")
            .Produces(StatusCodes.Status204NoContent)
            .WithName(nameof(LogoutHandler));
    }

    private async Task<IResult> HandleAsync()
    {
        await _signInManager.SignOutAsync();
        return TypedResults.NoContent();
    }
}