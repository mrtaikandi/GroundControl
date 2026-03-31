using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Variables;

internal sealed class UpdateVariableHandler : IEndpointHandler
{
    private readonly IVariableStore _store;
    private readonly AuditRecorder _audit;

    public UpdateVariableHandler(IVariableStore store, AuditRecorder audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
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

        var oldValues = variable.Values.ToList();
        var oldIsSensitive = variable.IsSensitive;
        var oldDescription = variable.Description;
        var isSensitive = variable.IsSensitive || request.IsSensitive;

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

        List<FieldChange> changes = [
            .. AuditRecorder.CompareCollections("Values", oldValues, variable.Values.ToList(), isSensitive),
            .. AuditRecorder.CompareFields("IsSensitive", oldIsSensitive.ToString(), variable.IsSensitive.ToString()),
            .. AuditRecorder.CompareFields("Description", oldDescription, variable.Description),
        ];

        await _audit.RecordAsync("Variable", variable.Id, variable.GroupId, "Updated", changes, cancellationToken: cancellationToken).ConfigureAwait(false);

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(variable.Version);
        return TypedResults.Ok(VariableResponse.From(variable));
    }
}