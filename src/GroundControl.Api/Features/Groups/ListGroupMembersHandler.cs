using GroundControl.Api.Features.Users.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Groups;

internal sealed class ListGroupMembersHandler : IEndpointHandler
{
    private readonly IGroupStore _groupStore;
    private readonly IUserStore _userStore;

    public ListGroupMembersHandler(IGroupStore groupStore, IUserStore userStore)
    {
        _groupStore = groupStore ?? throw new ArgumentNullException(nameof(groupStore));
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}/members", async (
                Guid id,
                [FromServices] ListGroupMembersHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, cancellationToken))
            .RequireAuthorization(Permissions.GroupsRead)
            .WithName(nameof(ListGroupMembersHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var group = await _groupStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return TypedResults.Problem(detail: $"Group '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        var members = await _userStore.GetByGroupAsync(id, cancellationToken).ConfigureAwait(false);
        var response = members.Select(UserResponse.From).ToList();

        return TypedResults.Ok(response);
    }
}