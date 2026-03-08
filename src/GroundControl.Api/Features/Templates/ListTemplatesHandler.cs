using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Templates;

internal sealed class ListTemplatesHandler : IEndpointHandler
{
    private readonly ITemplateStore _store;

    public ListTemplatesHandler(ITemplateStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(string.Empty, async (
                [AsParameters] TemplateListQuery query,
                [FromServices] ListTemplatesHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(query, cancellationToken))
            .RequireAuthorization(Permissions.TemplatesRead)
            .WithName(nameof(ListTemplatesHandler));
    }

    private async Task<IResult> HandleAsync(TemplateListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            var result = await _store.ListAsync(query, cancellationToken).ConfigureAwait(false);
            return TypedResults.Ok(new PaginatedResponse<TemplateResponse>
            {
                Data = result.Items.Select(TemplateResponse.From).ToList(),
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