using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.ConfigEntries;

internal sealed class ListConfigEntriesHandler : IEndpointHandler
{
    private readonly IConfigEntryStore _store;

    public ListConfigEntriesHandler(IConfigEntryStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(string.Empty, async (
                [AsParameters] ConfigEntryPaginationQuery query,
                [FromServices] ListConfigEntriesHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(query, cancellationToken))
            .RequireAuthorization(Permissions.ConfigEntriesRead)
            .WithName(nameof(ListConfigEntriesHandler));
    }

    private async Task<IResult> HandleAsync(ConfigEntryPaginationQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var storeQuery = query.ToStoreQuery();
            var result = await _store.ListAsync(storeQuery, cancellationToken).ConfigureAwait(false);

            return TypedResults.Ok(new PaginatedResponse<ConfigEntryResponse>
            {
                Data = result.Items.Select(ConfigEntryResponse.From).ToList(),
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