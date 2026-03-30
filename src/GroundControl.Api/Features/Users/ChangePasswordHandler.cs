using System.Security.Claims;
using AspNetCore.Identity.MongoDbCore.Models;
using GroundControl.Api.Features.Users.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Features.Users;

internal sealed class ChangePasswordHandler : IEndpointHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AuditRecorder _audit;
    private readonly AuthenticationOptions _options;

    public ChangePasswordHandler(IServiceProvider serviceProvider, AuditRecorder audit, IOptions<AuthenticationOptions> options)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _options = options.Value;
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}/password", async (
                Guid id,
                ChangePasswordRequest request,
                HttpContext httpContext,
                [FromServices] ChangePasswordHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, request, httpContext, cancellationToken))
            .RequireAuthorization()
            .WithName(nameof(ChangePasswordHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, ChangePasswordRequest request, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(httpContext);

        // Self only
        var sub = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var callerId) || callerId != id)
        {
            return TypedResults.Problem(detail: "Forbidden.", statusCode: StatusCodes.Status403Forbidden);
        }

        // BuiltIn mode only
        if (_options.Mode is not AuthenticationMode.BuiltIn)
        {
            return TypedResults.Problem(
                detail: "Password change is only available in BuiltIn authentication mode.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var userManager = _serviceProvider.GetRequiredService<UserManager<MongoIdentityUser<Guid>>>();
        var identityUser = await userManager.FindByIdAsync(id.ToString()).ConfigureAwait(false);
        if (identityUser is null)
        {
            return TypedResults.Problem(detail: $"User '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        var result = await userManager.ChangePasswordAsync(identityUser, request.CurrentPassword, request.NewPassword).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return TypedResults.Problem(
                detail: errors,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        await _audit.RecordAsync("User", id, null, "PasswordChanged", cancellationToken: cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}