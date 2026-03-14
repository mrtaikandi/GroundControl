using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Groups;

internal sealed class UpdateGroupHandler : IEndpointHandler
{
    private readonly IGroupStore _store;

    public UpdateGroupHandler(IGroupStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
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

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion))
        {
            return TypedResults.Problem(detail: "If-Match header is required.", statusCode: StatusCodes.Status428PreconditionRequired);
        }

        group.Name = request.Name;
        group.Description = request.Description;
        group.UpdatedAt = DateTimeOffset.UtcNow;
        group.UpdatedBy = Guid.Empty;

        var updated = await _store.UpdateAsync(group, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(group.Version);
        return TypedResults.Ok(GroupResponse.From(group));
    }
}