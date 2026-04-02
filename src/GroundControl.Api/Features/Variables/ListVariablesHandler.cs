using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Variables;

internal sealed class ListVariablesHandler : IEndpointHandler
{
    private readonly IVariableStore _store;
    private readonly SensitiveValueMasker _masker;

    public ListVariablesHandler(IVariableStore store, SensitiveValueMasker masker)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _masker = masker ?? throw new ArgumentNullException(nameof(masker));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(string.Empty, async (
                [AsParameters] VariablePaginationQuery query,
                HttpContext httpContext,
                [FromQuery] bool? decrypt,
                [FromServices] ListVariablesHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(query, decrypt == true, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.VariablesRead)
            .WithSummary("List variables")
            .WithDescription("Returns a paginated list of variables. Optionally decrypts sensitive values.")
            .Produces<PaginatedResponse<VariableResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithName(nameof(ListVariablesHandler));
    }

    private async Task<IResult> HandleAsync(
        VariablePaginationQuery query,
        bool decrypt,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var storeQuery = query.ToStoreQuery();
            var result = await _store.ListAsync(storeQuery, cancellationToken).ConfigureAwait(false);
            var canDecrypt = SensitiveValueMasker.CanDecrypt(httpContext, decrypt);

            var data = new List<VariableResponse>(result.Items.Count);
            foreach (var variable in result.Items)
            {
                var maskedValues = await _masker.MaskOrDecryptAsync(
                    variable.Values, variable.IsSensitive, canDecrypt, "Variable", variable.Id, variable.GroupId, cancellationToken).ConfigureAwait(false);

                data.Add(VariableResponse.From(variable, maskedValues));
            }

            return TypedResults.Ok(new PaginatedResponse<VariableResponse>
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