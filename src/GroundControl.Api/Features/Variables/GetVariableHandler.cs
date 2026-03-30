using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Variables;

internal sealed class GetVariableHandler : IEndpointHandler
{
    private readonly IVariableStore _store;
    private readonly SensitiveValueMasker _masker;

    public GetVariableHandler(IVariableStore store, SensitiveValueMasker masker)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _masker = masker ?? throw new ArgumentNullException(nameof(masker));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                [FromQuery] bool? decrypt,
                [FromServices] GetVariableHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, decrypt == true, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.VariablesRead)
            .WithName(nameof(GetVariableHandler));
    }

    private async Task<IResult> HandleAsync(
        Guid id,
        bool decrypt,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var variable = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (variable is null)
        {
            return TypedResults.Problem(detail: $"Variable '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        var canDecrypt = SensitiveValueMasker.CanDecrypt(httpContext, decrypt);
        var maskedValues = await _masker.MaskOrDecryptAsync(
            variable.Values, variable.IsSensitive, canDecrypt, "Variable", variable.Id, variable.GroupId, cancellationToken).ConfigureAwait(false);

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(variable.Version);
        return TypedResults.Ok(VariableResponse.From(variable, maskedValues));
    }
}