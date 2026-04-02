using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Api.Shared.Security;
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
                [AsParameters] TemplatePaginationQuery query,
                [FromServices] ListTemplatesHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(query, cancellationToken))
            .RequireAuthorization(Permissions.TemplatesRead)
            .WithSummary("List templates")
            .WithDescription("Returns a paginated list of configuration templates.")
            .Produces<PaginatedResponse<TemplateResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithName(nameof(ListTemplatesHandler));
    }

    private async Task<IResult> HandleAsync(TemplatePaginationQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var storeQuery = query.ToStoreQuery();
            var result = await _store.ListAsync(storeQuery, cancellationToken).ConfigureAwait(false);

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