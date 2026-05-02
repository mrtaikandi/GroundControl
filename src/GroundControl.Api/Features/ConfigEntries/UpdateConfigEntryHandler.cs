using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.ConfigEntries;

internal sealed class UpdateConfigEntryHandler : IEndpointHandler
{
    private readonly IConfigEntryStore _store;
    private readonly AuditRecorder _audit;
    private readonly SensitiveSourceValueProtector _protector;

    public UpdateConfigEntryHandler(IConfigEntryStore store, AuditRecorder audit, SensitiveSourceValueProtector protector)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
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
        var oldIsSensitive = entry.IsSensitive;
        var oldDescription = entry.Description;
        var auditIsSensitive = oldIsSensitive || request.IsSensitive;

        // Decrypt the old values for change comparison so unchanged sensitive values do not produce
        // spurious audit records (ASP.NET Data Protection ciphertext is non-deterministic, so the
        // raw stored bytes always differ even when the underlying plaintext is identical).
        var oldPlaintextValues = _protector.UnprotectValues(entry.Values, oldIsSensitive);

        var newPlaintextValues = request.Values.Select(v => new ScopedValue(v.Value, v.Scopes)).ToList();
        var protectedValues = _protector.ProtectValues(newPlaintextValues, request.IsSensitive);

        entry.ValueType = request.ValueType;
        entry.Values.Clear();
        foreach (var v in protectedValues)
        {
            entry.Values.Add(v);
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
            .. AuditRecorder.CompareFields("ValueType", oldValueType, entry.ValueType),
            .. AuditRecorder.CompareCollections("Values", [.. oldPlaintextValues], newPlaintextValues, auditIsSensitive),
            .. AuditRecorder.CompareFields("IsSensitive", oldIsSensitive.ToString(), entry.IsSensitive.ToString()),
            .. AuditRecorder.CompareFields("Description", oldDescription, entry.Description),
        ];

        await _audit.RecordAsync("ConfigEntry", entry.Id, null, "Updated", changes, cancellationToken: cancellationToken).ConfigureAwait(false);

        var responseValues = _protector.MaskValues(entry.Values, entry.IsSensitive);
        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(entry.Version);

        return TypedResults.Ok(ConfigEntryResponse.From(entry, responseValues));
    }
}