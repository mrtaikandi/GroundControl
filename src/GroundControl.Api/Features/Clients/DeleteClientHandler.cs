using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Clients;

internal sealed class DeleteClientHandler : IEndpointHandler
{
    private readonly IClientStore _store;

    public DeleteClientHandler(IClientStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/{id:guid}", async (
                Guid projectId,
                Guid id,
                HttpContext httpContext,
                [FromServices] DeleteClientHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(projectId, id, httpContext, cancellationToken))
            .WithEndpointValidation<DeleteClientValidator>()
            .RequireAuthorization(Permissions.ClientsWrite)
            .WithName(nameof(DeleteClientHandler));
    }

    private async Task<IResult> HandleAsync(Guid projectId, Guid id, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion, out var problem))
        {
            return problem;
        }

        var client = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (client is null || client.ProjectId != projectId)
        {
            return TypedResults.Problem(detail: $"Client '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        var deleted = await _store.DeleteAsync(id, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        return TypedResults.NoContent();
    }
}