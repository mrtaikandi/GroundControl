using System.Security.Claims;
using GroundControl.Api.Features.Users.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Users;

internal sealed class UpdateUserHandler : IEndpointHandler
{
    private readonly IUserStore _userStore;
    private readonly IAuthorizationService _authorizationService;
    private readonly AuditRecorder _audit;

    public UpdateUserHandler(IUserStore userStore, IAuthorizationService authorizationService, AuditRecorder audit)
    {
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}", async (
                Guid id,
                UpdateUserRequest request,
                HttpContext httpContext,
                [FromServices] UpdateUserHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, request, httpContext, cancellationToken))
            .RequireAuthorization()
            .WithContractValidation<UpdateUserRequest>()
            .WithName(nameof(UpdateUserHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, UpdateUserRequest request, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(httpContext);

        var isSelf = IsSelf(httpContext, id);
        var hasUsersWrite = false;

        if (!isSelf)
        {
            var authResult = await _authorizationService.AuthorizeAsync(httpContext.User, Permissions.UsersWrite).ConfigureAwait(false);
            if (!authResult.Succeeded)
            {
                return TypedResults.Problem(detail: "Forbidden.", statusCode: StatusCodes.Status403Forbidden);
            }

            hasUsersWrite = true;
        }
        else
        {
            // Self-access: check if admin fields are being modified
            if (request.Grants is not null || request.IsActive is not null)
            {
                var authResult = await _authorizationService.AuthorizeAsync(httpContext.User, Permissions.UsersWrite).ConfigureAwait(false);
                if (!authResult.Succeeded)
                {
                    return TypedResults.Problem(
                        detail: "Updating grants or active status requires users:write permission.",
                        statusCode: StatusCodes.Status403Forbidden);
                }

                hasUsersWrite = true;
            }
        }

        var user = await _userStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return TypedResults.Problem(detail: $"User '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion, out var problem))
        {
            return problem;
        }

        var oldUsername = user.Username;
        var oldEmail = user.Email;
        var oldIsActive = user.IsActive;
        var oldGrants = user.Grants.ToList();

        // Apply allowed fields
        user.Username = request.Username;
        user.Email = request.Email;

        if (hasUsersWrite)
        {
            if (request.Grants is not null)
            {
                user.Grants.Clear();
                foreach (var grant in request.Grants)
                {
                    user.Grants.Add(grant.ToEntity());
                }
            }

            if (request.IsActive is not null)
            {
                user.IsActive = request.IsActive.Value;
            }
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = Guid.Empty;

        var updated = await _userStore.UpdateAsync(user, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        List<FieldChange> changes = [
            .. AuditRecorder.CompareFields("Username", oldUsername, user.Username),
            .. AuditRecorder.CompareFields("Email", oldEmail, user.Email),
            .. AuditRecorder.CompareFields("IsActive", oldIsActive.ToString(), user.IsActive.ToString()),
            .. AuditRecorder.CompareCollections("Grants", oldGrants, user.Grants.ToList()),
        ];

        await _audit.RecordAsync("User", user.Id, null, "Updated", changes, cancellationToken: cancellationToken).ConfigureAwait(false);

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(user.Version);
        return TypedResults.Ok(UserResponse.From(user));
    }

    private static bool IsSelf(HttpContext httpContext, Guid targetId)
    {
        var sub = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var callerId) && callerId == targetId;
    }
}