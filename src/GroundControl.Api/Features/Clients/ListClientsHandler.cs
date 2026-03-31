using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.Clients.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Clients;

internal sealed class ListClientsHandler : IEndpointHandler
{
    private readonly IClientStore _store;

    public ListClientsHandler(IClientStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(string.Empty, async (
                Guid projectId,
                [AsParameters] ClientPaginationQuery query,
                [FromServices] ListClientsHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(projectId, query, cancellationToken))
            .RequireAuthorization(Permissions.ClientsRead)
            .WithName(nameof(ListClientsHandler));
    }

    private async Task<IResult> HandleAsync(Guid projectId, ClientPaginationQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var storeQuery = query.ToStoreQuery(projectId);
            var result = await _store.ListAsync(storeQuery, cancellationToken).ConfigureAwait(false);

            return TypedResults.Ok(new PaginatedResponse<ClientResponse>
            {
                Data = result.Items.Select(ClientResponse.From).ToList(),
                NextCursor = result.NextCursor,
                PreviousCursor = result.PreviousCursor,
                TotalCount = result.TotalCount,
            });
        }
        catch (ValidationException validationException)
        {
            return TypedResults.Problem(
                detail: validationException.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}