using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.ConfigEntries;

internal sealed class UpdateConfigEntryHandler : IEndpointHandler
{
    private readonly IConfigEntryStore _store;
    private readonly IScopeStore _scopeStore;

    public UpdateConfigEntryHandler(IConfigEntryStore store, IScopeStore scopeStore)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _scopeStore = scopeStore ?? throw new ArgumentNullException(nameof(scopeStore));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}", async (
                Guid id,
                UpdateConfigEntryRequest request,
                HttpContext httpContext,
                [FromServices] UpdateConfigEntryHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, request, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.ConfigEntriesWrite)
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

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion))
        {
            return TypedResults.Problem(detail: "If-Match header is required.", statusCode: StatusCodes.Status428PreconditionRequired);
        }

        if (!ConfigEntryValidation.IsValidValueType(request.ValueType))
        {
            return TypedResults.Problem(
                detail: $"ValueType '{request.ValueType}' is not supported.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        foreach (var scopedValue in request.Values)
        {
            var valueError = ConfigEntryValidation.ValidateValue(scopedValue.Value, request.ValueType);
            if (valueError is not null)
            {
                return TypedResults.Problem(detail: valueError, statusCode: StatusCodes.Status400BadRequest);
            }
        }

        var scopeError = await ConfigEntryValidation.ValidateScopesAsync(request.Values, _scopeStore, cancellationToken).ConfigureAwait(false);
        if (scopeError is not null)
        {
            return TypedResults.Problem(detail: scopeError, statusCode: StatusCodes.Status400BadRequest);
        }

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

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(entry.Version);
        return TypedResults.Ok(ConfigEntryResponse.From(entry));
    }
}