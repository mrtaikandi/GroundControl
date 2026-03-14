using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Variables;

internal sealed class UpdateVariableHandler : IEndpointHandler
{
    private readonly IVariableStore _store;

    public UpdateVariableHandler(IVariableStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}", async (
                Guid id,
                UpdateVariableRequest request,
                HttpContext httpContext,
                [FromServices] UpdateVariableHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, request, httpContext, cancellationToken))
            .WithContractValidation<UpdateVariableRequest>()
            .RequireAuthorization(Permissions.VariablesWrite)
            .WithName(nameof(UpdateVariableHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, UpdateVariableRequest request, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(httpContext);

        var variable = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (variable is null)
        {
            return TypedResults.Problem(detail: $"Variable '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion, out var problem))
        {
            return problem;
        }

        variable.Values.Clear();
        foreach (var v in request.Values)
        {
            variable.Values.Add(new ScopedValue { Scopes = v.Scopes, Value = v.Value });
        }

        variable.IsSensitive = request.IsSensitive;
        variable.Description = request.Description;
        variable.UpdatedAt = DateTimeOffset.UtcNow;
        variable.UpdatedBy = Guid.Empty;

        var updated = await _store.UpdateAsync(variable, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(variable.Version);
        return TypedResults.Ok(VariableResponse.From(variable));
    }
}