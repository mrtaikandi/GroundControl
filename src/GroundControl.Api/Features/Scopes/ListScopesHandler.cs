using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Scopes;

internal sealed class ListScopesHandler : IEndpointHandler
{
    private readonly IScopeStore _store;

    public ListScopesHandler(IScopeStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(string.Empty, async (
                [AsParameters] PaginationQuery query,
                [FromServices] ListScopesHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(query, cancellationToken))
            .RequireAuthorization(Permissions.ScopesRead)
            .WithSummary("List scopes")
            .WithDescription("Returns a paginated list of scope definitions.")
            .Produces<PaginatedResponse<ScopeResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithName(nameof(ListScopesHandler));
    }

    private async Task<IResult> HandleAsync(PaginationQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var storeQuery = query.ToStoreQuery();
            var result = await _store.ListAsync(storeQuery, cancellationToken).ConfigureAwait(false);

            return TypedResults.Ok(new PaginatedResponse<ScopeResponse>
            {
                Data = result.Items.Select(ScopeResponse.From).ToList(),
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