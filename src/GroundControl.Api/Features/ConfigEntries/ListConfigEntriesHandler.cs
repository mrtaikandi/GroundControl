using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.ConfigEntries;

internal sealed class ListConfigEntriesHandler : IEndpointHandler
{
    private readonly IConfigEntryStore _store;
    private readonly SensitiveValueMasker _masker;

    public ListConfigEntriesHandler(IConfigEntryStore store, SensitiveValueMasker masker)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _masker = masker ?? throw new ArgumentNullException(nameof(masker));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(string.Empty, async (
                [AsParameters] ConfigEntryPaginationQuery query,
                HttpContext httpContext,
                [FromQuery] bool? decrypt,
                [FromServices] ListConfigEntriesHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(query, decrypt == true, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.ConfigEntriesRead)
            .WithName(nameof(ListConfigEntriesHandler));
    }

    private async Task<IResult> HandleAsync(
        ConfigEntryPaginationQuery query,
        bool decrypt,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var storeQuery = query.ToStoreQuery();
            var result = await _store.ListAsync(storeQuery, cancellationToken).ConfigureAwait(false);
            var canDecrypt = SensitiveValueMasker.CanDecrypt(httpContext, decrypt);

            var data = new List<ConfigEntryResponse>(result.Items.Count);
            foreach (var entry in result.Items)
            {
                var maskedValues = await _masker.MaskOrDecryptAsync(
                    entry.Values, entry.IsSensitive, canDecrypt, "ConfigEntry", entry.Id, null, cancellationToken).ConfigureAwait(false);

                data.Add(ConfigEntryResponse.From(entry, maskedValues));
            }

            return TypedResults.Ok(new PaginatedResponse<ConfigEntryResponse>
            {
                Data = data,
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