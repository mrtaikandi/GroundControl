using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.ConfigEntries;

internal sealed class CreateConfigEntryHandler : IEndpointHandler
{
    private readonly IConfigEntryStore _store;
    private readonly AuditRecorder _audit;

    public CreateConfigEntryHandler(IConfigEntryStore store, AuditRecorder audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(string.Empty, async (
                CreateConfigEntryRequest request,
                [FromServices] CreateConfigEntryHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(request, cancellationToken))
            .WithContractValidation<CreateConfigEntryRequest>()
            .RequireAuthorization(Permissions.ConfigEntriesWrite)
            .WithName(nameof(CreateConfigEntryHandler));
    }

    private async Task<IResult> HandleAsync(CreateConfigEntryRequest request, CancellationToken cancellationToken = default)
    {
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

        await _audit.RecordAsync("ConfigEntry", entry.Id, null, "Created", cancellationToken: cancellationToken).ConfigureAwait(false);

        return TypedResults.Created($"/api/config-entries/{entry.Id}", ConfigEntryResponse.From(entry));
    }
}