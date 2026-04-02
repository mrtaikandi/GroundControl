using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Groups;

internal sealed class RemoveGroupMemberHandler : IEndpointHandler
{
    private readonly IGroupStore _groupStore;
    private readonly IUserStore _userStore;
    private readonly AuditRecorder _audit;

    public RemoveGroupMemberHandler(IGroupStore groupStore, IUserStore userStore, AuditRecorder audit)
    {
        _groupStore = groupStore ?? throw new ArgumentNullException(nameof(groupStore));
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/{id:guid}/members/{userId:guid}", async (
                Guid id,
                Guid userId,
                [FromServices] RemoveGroupMemberHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, userId, cancellationToken))
            .RequireAuthorization(Permissions.GroupsWrite)
            .WithSummary("Remove a group member")
            .WithDescription("Removes a user from the specified group.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(RemoveGroupMemberHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
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

        var grantsToRemove = user.Grants.Where(g => g.Resource == id).ToList();
        if (grantsToRemove.Count == 0)
        {
            return TypedResults.NoContent();
        }

        foreach (var grant in grantsToRemove)
        {
            user.Grants.Remove(grant);
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = Guid.Empty;

        var updated = await _userStore.UpdateAsync(user, user.Version, cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        var metadata = new Dictionary<string, string> { ["GroupId"] = id.ToString() };
        await _audit.RecordAsync("User", userId, id, "GrantRemoved", metadata: metadata, cancellationToken: cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}