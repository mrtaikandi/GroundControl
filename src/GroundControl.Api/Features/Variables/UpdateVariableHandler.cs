using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Variables;

internal sealed class UpdateVariableHandler : IEndpointHandler
{
    private readonly IVariableStore _store;
    private readonly AuditRecorder _audit;
    private readonly SensitiveSourceValueProtector _protector;

    public UpdateVariableHandler(IVariableStore store, AuditRecorder audit, SensitiveSourceValueProtector protector)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
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
            .WithSummary("Update a variable")
            .WithDescription("Updates an existing variable. Requires an If-Match header with the current ETag value.")
            .Produces<VariableResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
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

        var oldIsSensitive = variable.IsSensitive;
        var oldDescription = variable.Description;
        var auditIsSensitive = oldIsSensitive || request.IsSensitive;

        var oldPlaintextValues = _protector.UnprotectValues(variable.Values, oldIsSensitive);

        var newPlaintextValues = request.Values.Select(v => new ScopedValue(v.Value, v.Scopes)).ToList();
        var protectedValues = _protector.ProtectValues(newPlaintextValues, request.IsSensitive);

        variable.Values.Clear();
        foreach (var v in protectedValues)
        {
            variable.Values.Add(v);
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
            .. AuditRecorder.CompareCollections("Values", [.. oldPlaintextValues], newPlaintextValues, auditIsSensitive),
            .. AuditRecorder.CompareFields("IsSensitive", oldIsSensitive.ToString(), variable.IsSensitive.ToString()),
            .. AuditRecorder.CompareFields("Description", oldDescription, variable.Description),
        ];

        await _audit.RecordAsync("Variable", variable.Id, variable.GroupId, "Updated", changes, cancellationToken: cancellationToken).ConfigureAwait(false);

        var responseValues = SensitiveSourceValueProtector.MaskValues(variable.Values, variable.IsSensitive);
        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(variable.Version);
        return TypedResults.Ok(VariableResponse.From(variable, [.. responseValues]));
    }
}