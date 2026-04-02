using AspNetCore.Identity.MongoDbCore.Models;
using GroundControl.Api.Core.Authentication;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Features.Users;

internal sealed class DeleteUserHandler : IEndpointHandler
{
    private readonly IUserStore _userStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly AuditRecorder _audit;
    private readonly AuthenticationOptions _options;

    public DeleteUserHandler(IUserStore userStore, IServiceProvider serviceProvider, AuditRecorder audit, IOptions<AuthenticationOptions> options)
    {
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _options = options.Value;
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                [FromServices] DeleteUserHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.UsersWrite)
            .WithSummary("Delete a user")
            .WithDescription("Deletes a user account. Requires an If-Match header.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
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
        if (_options.Mode is AuthenticationMode.BuiltIn)
        {
            var userManager = _serviceProvider.GetRequiredService<UserManager<MongoIdentityUser<Guid>>>();
            var identityUser = await userManager.FindByIdAsync(id.ToString()).ConfigureAwait(false);
            if (identityUser is not null)
            {
                await userManager.SetLockoutEnabledAsync(identityUser, true).ConfigureAwait(false);
                await userManager.SetLockoutEndDateAsync(identityUser, DateTimeOffset.MaxValue).ConfigureAwait(false);
            }
        }

        await _audit.RecordAsync("User", id, null, "Deactivated", cancellationToken: cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}