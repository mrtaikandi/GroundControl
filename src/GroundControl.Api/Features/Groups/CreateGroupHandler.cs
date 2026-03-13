using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Groups;

internal sealed class CreateGroupHandler : IEndpointHandler
{
    private readonly IGroupStore _store;

    public CreateGroupHandler(IGroupStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(string.Empty, async (
                CreateGroupRequest request,
                [FromServices] CreateGroupHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(request, cancellationToken))
            .WithValidationOn<CreateGroupRequest>()
            .RequireAuthorization(Permissions.GroupsWrite)
            .WithName(nameof(CreateGroupHandler));
    }

    private async Task<IResult> HandleAsync(CreateGroupRequest request, CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var group = new Group
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            Description = request.Description,
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty,
        };

        await _store.CreateAsync(group, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created($"/api/groups/{group.Id}", GroupResponse.From(group));
    }
}