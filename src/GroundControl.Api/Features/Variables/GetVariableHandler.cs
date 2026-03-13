using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Variables;

internal sealed class GetVariableHandler : IEndpointHandler
{
    private readonly IVariableStore _store;

    public GetVariableHandler(IVariableStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                [FromServices] GetVariableHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.VariablesRead)
            .WithName(nameof(GetVariableHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var variable = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (variable is null)
        {
            return TypedResults.Problem(detail: $"Variable '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(variable.Version);
        return TypedResults.Ok(VariableResponse.From(variable));
    }
}