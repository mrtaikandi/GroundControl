using GroundControl.Api.Features.Clients.Contracts;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Clients;

internal sealed class GetClientHandler : IEndpointHandler
{
    private readonly IClientStore _store;

    public GetClientHandler(IClientStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", async (
                Guid projectId,
                Guid id,
                HttpContext httpContext,
                [FromServices] GetClientHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(projectId, id, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.ClientsRead)
            .WithSummary("Get a client")
            .WithDescription("Returns a client by its unique identifier. Includes an ETag header for optimistic concurrency.")
            .Produces<ClientResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName(nameof(GetClientHandler));
    }

    private async Task<IResult> HandleAsync(Guid projectId, Guid id, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var client = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (client is null || client.ProjectId != projectId)
        {
            return TypedResults.Problem(detail: $"Client '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(client.Version);
        return TypedResults.Ok(ClientResponse.From(client));
    }
}