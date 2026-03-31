using AspNetCore.Identity.MongoDbCore.Models;
using GroundControl.Api.Core.Authentication.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Core.Authentication;

internal sealed class LoginHandler : IEndpointHandler
{
    private readonly SignInManager<MongoIdentityUser<Guid>> _signInManager;
    private readonly IUserStore _userStore;

    public LoginHandler(SignInManager<MongoIdentityUser<Guid>> signInManager, IUserStore userStore)
    {
        _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/login", async (
                LoginRequest request,
                [FromServices] LoginHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(request, httpContext, cancellationToken))
            .AllowAnonymous()
            .WithName(nameof(LoginHandler));
    }

    private async Task<IResult> HandleAsync(LoginRequest request, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var result = await _signInManager.PasswordSignInAsync(
            request.Username, request.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return TypedResults.Problem(
                detail: "Account is locked out due to too many failed attempts. Try again later.",
                statusCode: StatusCodes.Status423Locked);
        }

        if (!result.Succeeded)
        {
            return TypedResults.Problem(
                detail: "Invalid username or password.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var user = await _userStore.GetByUsernameAsync(request.Username, cancellationToken);
        if (user is null)
        {
            return TypedResults.Problem(
                detail: "User account not found.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // Sign in via cookie scheme — SignInManager already set the cookie through its default sign-in scheme
        return TypedResults.Ok(UserResponse.From(user));
    }
}