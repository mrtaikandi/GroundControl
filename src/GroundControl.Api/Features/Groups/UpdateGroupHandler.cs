using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Groups;

internal sealed class UpdateGroupHandler : IEndpointHandler
{
    private readonly IGroupStore _store;
    private readonly AuditRecorder _audit;

    public UpdateGroupHandler(IGroupStore store, AuditRecorder audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}", async (
                Guid id,
                UpdateGroupRequest request,
                HttpContext httpContext,
                [FromServices] UpdateGroupHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, request, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.GroupsWrite)
            .WithContractValidation<UpdateGroupRequest>()
            .WithSummary("Update a group")
            .WithDescription("Updates an existing group. Requires an If-Match header with the current ETag value.")
            .Produces<GroupResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .WithName(nameof(UpdateGroupHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, UpdateGroupRequest request, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(httpContext);

        var group = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return TypedResults.Problem(detail: $"Group '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion, out var problem))
        {
            return problem;
        }

        var oldName = group.Name;
        var oldDescription = group.Description;

        group.Name = request.Name;
        group.Description = request.Description;
        group.UpdatedAt = DateTimeOffset.UtcNow;
        group.UpdatedBy = Guid.Empty;

        var updated = await _store.UpdateAsync(group, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        List<FieldChange> changes = [
            .. AuditRecorder.CompareFields("Name", oldName, group.Name),
            .. AuditRecorder.CompareFields("Description", oldDescription, group.Description),
        ];

        await _audit.RecordAsync("Group", group.Id, null, "Updated", changes, cancellationToken: cancellationToken).ConfigureAwait(false);

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(group.Version);
        return TypedResults.Ok(GroupResponse.From(group));
    }
}