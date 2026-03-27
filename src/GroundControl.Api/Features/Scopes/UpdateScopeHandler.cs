using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Scopes;

internal sealed class UpdateScopeHandler : IEndpointHandler
{
    private readonly IScopeStore _store;
    private readonly AuditRecorder _audit;

    public UpdateScopeHandler(IScopeStore store, AuditRecorder audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}", async (
                Guid id,
                UpdateScopeRequest request,
                HttpContext httpContext,
                [FromServices] UpdateScopeHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, request, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.ScopesWrite)
            .WithContractValidation<UpdateScopeRequest>()
            .WithName(nameof(UpdateScopeHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, UpdateScopeRequest request, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(httpContext);

        var scope = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (scope is null)
        {
            return TypedResults.Problem(detail: $"Scope '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion, out var problem))
        {
            return problem;
        }

        var oldDimension = scope.Dimension;
        var oldAllowedValues = scope.AllowedValues.ToList();
        var oldDescription = scope.Description;

        scope.Dimension = request.Dimension;
        scope.AllowedValues.Clear();

        foreach (var allowedValue in request.AllowedValues)
        {
            scope.AllowedValues.Add(allowedValue);
        }

        scope.Description = request.Description;
        scope.UpdatedAt = DateTimeOffset.UtcNow;
        scope.UpdatedBy = Guid.Empty;

        var updated = await _store.UpdateAsync(scope, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        List<FieldChange> changes = [
            .. AuditRecorder.CompareFields("Dimension", oldDimension, scope.Dimension),
            .. AuditRecorder.CompareCollections("AllowedValues", oldAllowedValues, scope.AllowedValues.ToList()),
            .. AuditRecorder.CompareFields("Description", oldDescription, scope.Description),
        ];

        await _audit.RecordAsync("Scope", scope.Id, null, "Updated", changes, cancellationToken: cancellationToken).ConfigureAwait(false);

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(scope.Version);
        return TypedResults.Ok(ScopeResponse.From(scope));
    }
}