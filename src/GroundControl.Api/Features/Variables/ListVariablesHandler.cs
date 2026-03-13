using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Variables;

internal sealed class ListVariablesHandler : IEndpointHandler
{
    private readonly IVariableStore _store;

    public ListVariablesHandler(IVariableStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(string.Empty, async (
                [AsParameters] VariableListQuery query,
                [FromServices] ListVariablesHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(query, cancellationToken))
            .RequireAuthorization(Permissions.VariablesRead)
            .WithName(nameof(ListVariablesHandler));
    }

    private async Task<IResult> HandleAsync(VariableListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            var result = await _store.ListAsync(query, cancellationToken).ConfigureAwait(false);
            return TypedResults.Ok(new PaginatedResponse<VariableResponse>
            {
                Data = result.Items.Select(VariableResponse.From).ToList(),
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