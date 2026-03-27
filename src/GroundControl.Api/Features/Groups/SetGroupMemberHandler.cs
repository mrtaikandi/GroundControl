using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Groups;

internal sealed class SetGroupMemberHandler : IEndpointHandler
{
    private readonly IGroupStore _groupStore;
    private readonly IUserStore _userStore;
    private readonly AuditRecorder _audit;

    public SetGroupMemberHandler(IGroupStore groupStore, IUserStore userStore, AuditRecorder audit)
    {
        _groupStore = groupStore ?? throw new ArgumentNullException(nameof(groupStore));
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}/members/{userId:guid}", async (
                Guid id,
                Guid userId,
                SetGroupMemberRequest request,
                [FromServices] SetGroupMemberHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, userId, request, cancellationToken))
            .RequireAuthorization(Permissions.GroupsWrite)
            .WithName(nameof(SetGroupMemberHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, Guid userId, SetGroupMemberRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var group = await _groupStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return TypedResults.Problem(detail: $"Group '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        var user = await _userStore.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return TypedResults.Problem(detail: $"User '{userId}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        // Idempotent: check if grant already exists
        var existingGrant = user.Grants.FirstOrDefault(g => g.Resource == id && g.RoleId == request.RoleId);
        if (existingGrant is not null)
        {
            return TypedResults.NoContent();
        }

        user.Grants.Add(new Grant { Resource = id, RoleId = request.RoleId });
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = Guid.Empty;

        var updated = await _userStore.UpdateAsync(user, user.Version, cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        var metadata = new Dictionary<string, string>
        {
            ["GroupId"] = id.ToString(),
            ["RoleId"] = request.RoleId.ToString(),
        };

        await _audit.RecordAsync("User", userId, id, "GrantAdded", metadata: metadata, cancellationToken: cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}