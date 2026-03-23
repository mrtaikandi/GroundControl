using AspNetCore.Identity.MongoDbCore.Models;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Auth;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Users;

internal sealed class DeleteUserHandler : IEndpointHandler
{
    private readonly IUserStore _userStore;
    private readonly IAuthConfigurator _authConfigurator;
    private readonly IServiceProvider _serviceProvider;

    public DeleteUserHandler(IUserStore userStore, IAuthConfigurator authConfigurator, IServiceProvider serviceProvider)
    {
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
        _authConfigurator = authConfigurator ?? throw new ArgumentNullException(nameof(authConfigurator));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                [FromServices] DeleteUserHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.UsersWrite)
            .WithName(nameof(DeleteUserHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion, out var problem))
        {
            return problem;
        }

        var user = await _userStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return TypedResults.Problem(detail: $"User '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        // Soft-delete: set IsActive=false
        user.IsActive = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = Guid.Empty;

        var updated = await _userStore.UpdateAsync(user, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        // In BuiltIn mode, also lock the identity user to prevent login
        if (_authConfigurator is BuiltInAuthConfigurator)
        {
            var userManager = _serviceProvider.GetRequiredService<UserManager<MongoIdentityUser<Guid>>>();
            var identityUser = await userManager.FindByIdAsync(id.ToString()).ConfigureAwait(false);
            if (identityUser is not null)
            {
                await userManager.SetLockoutEnabledAsync(identityUser, true).ConfigureAwait(false);
                await userManager.SetLockoutEndDateAsync(identityUser, DateTimeOffset.MaxValue).ConfigureAwait(false);
            }
        }

        return TypedResults.NoContent();
    }
}