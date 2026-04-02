using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Groups;

internal sealed class CreateGroupHandler : IEndpointHandler
{
    private readonly IGroupStore _store;
    private readonly AuditRecorder _audit;

    public CreateGroupHandler(IGroupStore store, AuditRecorder audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(string.Empty, async (
                CreateGroupRequest request,
                [FromServices] CreateGroupHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(request, cancellationToken))
            .WithContractValidation<CreateGroupRequest>()
            .RequireAuthorization(Permissions.GroupsWrite)
            .WithSummary("Create a group")
            .WithDescription("Creates a new user group.")
            .Produces<GroupResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
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

        await _audit.RecordAsync("Group", group.Id, null, "Created", cancellationToken: cancellationToken).ConfigureAwait(false);

        return TypedResults.Created($"/api/groups/{group.Id}", GroupResponse.From(group));
    }
}