using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.ConfigEntries;

internal sealed class UpdateConfigEntryHandler : IEndpointHandler
{
    private readonly IConfigEntryStore _store;
    private readonly AuditRecorder _audit;

    public UpdateConfigEntryHandler(IConfigEntryStore store, AuditRecorder audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}", async (
                Guid id,
                UpdateConfigEntryRequest request,
                HttpContext httpContext,
                [FromServices] UpdateConfigEntryHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, request, httpContext, cancellationToken))
            .WithContractValidation<UpdateConfigEntryRequest>()
            .RequireAuthorization(Permissions.ConfigEntriesWrite)
            .WithSummary("Update a configuration entry")
            .WithDescription("Updates an existing configuration entry. Requires an If-Match header with the current ETag value.")
            .Produces<ConfigEntryResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .WithName(nameof(UpdateConfigEntryHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, UpdateConfigEntryRequest request, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(httpContext);

        var entry = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return TypedResults.Problem(detail: $"Config entry '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion, out var problem))
        {
            return problem;
        }

        var oldValueType = entry.ValueType;
        var oldValues = entry.Values.ToList();
        var oldIsSensitive = entry.IsSensitive;
        var oldDescription = entry.Description;
        var isSensitive = entry.IsSensitive || request.IsSensitive;

        entry.ValueType = request.ValueType;
        entry.Values.Clear();
        foreach (var v in request.Values)
        {
            entry.Values.Add(new ScopedValue { Scopes = v.Scopes, Value = v.Value });
        }

        entry.IsSensitive = request.IsSensitive;
        entry.Description = request.Description;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.UpdatedBy = Guid.Empty;

        var updated = await _store.UpdateAsync(entry, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        List<FieldChange> changes = [
            .. AuditRecorder.CompareFields("ValueType", oldValueType.ToString(), entry.ValueType.ToString()),
            .. AuditRecorder.CompareCollections("Values", oldValues, entry.Values.ToList(), isSensitive),
            .. AuditRecorder.CompareFields("IsSensitive", oldIsSensitive.ToString(), entry.IsSensitive.ToString()),
            .. AuditRecorder.CompareFields("Description", oldDescription, entry.Description),
        ];

        await _audit.RecordAsync("ConfigEntry", entry.Id, null, "Updated", changes, cancellationToken: cancellationToken).ConfigureAwait(false);

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(entry.Version);
        return TypedResults.Ok(ConfigEntryResponse.From(entry));
    }
}