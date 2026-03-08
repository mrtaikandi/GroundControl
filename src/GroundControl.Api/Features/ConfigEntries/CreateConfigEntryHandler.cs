using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.ConfigEntries;

internal sealed class CreateConfigEntryHandler : IEndpointHandler
{
    private readonly IConfigEntryStore _store;
    private readonly IScopeStore _scopeStore;

    public CreateConfigEntryHandler(IConfigEntryStore store, IScopeStore scopeStore)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _scopeStore = scopeStore ?? throw new ArgumentNullException(nameof(scopeStore));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(string.Empty, async (
                CreateConfigEntryRequest request,
                [FromServices] CreateConfigEntryHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(request, cancellationToken))
            .RequireAuthorization(Permissions.ConfigEntriesWrite)
            .WithName(nameof(CreateConfigEntryHandler));
    }

    private async Task<IResult> HandleAsync(CreateConfigEntryRequest request, CancellationToken cancellationToken = default)
    {
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

        var timestamp = DateTimeOffset.UtcNow;
        var entry = new ConfigEntry
        {
            Id = Guid.CreateVersion7(),
            Key = request.Key,
            OwnerId = request.OwnerId,
            OwnerType = request.OwnerType,
            ValueType = request.ValueType,
            Values = [.. request.Values.Select(v => new ScopedValue(v.Value, v.Scopes))],
            IsSensitive = request.IsSensitive,
            Description = request.Description,
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty,
        };

        try
        {
            await _store.CreateAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateKeyException)
        {
            return TypedResults.Problem(
                detail: $"A config entry with key '{request.Key}' already exists for this owner.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return TypedResults.Created($"/api/config-entries/{entry.Id}", ConfigEntryResponse.From(entry));
    }
}