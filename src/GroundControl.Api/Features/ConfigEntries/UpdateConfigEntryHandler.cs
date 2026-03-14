using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.ConfigEntries;

internal sealed class UpdateConfigEntryHandler : IEndpointHandler
{
    private readonly IConfigEntryStore _store;

    public UpdateConfigEntryHandler(IConfigEntryStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
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