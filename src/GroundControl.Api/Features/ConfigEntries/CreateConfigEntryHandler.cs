using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.ConfigEntries;

internal sealed class CreateConfigEntryHandler : IEndpointHandler
{
    private readonly IConfigEntryStore _store;
    private readonly AuditRecorder _audit;
    private readonly SensitiveSourceValueProtector _protector;

    public CreateConfigEntryHandler(IConfigEntryStore store, AuditRecorder audit, SensitiveSourceValueProtector protector)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(string.Empty, async (
                CreateConfigEntryRequest request,
                [FromServices] CreateConfigEntryHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(request, cancellationToken))
            .WithContractValidation<CreateConfigEntryRequest>()
            .RequireAuthorization(Permissions.ConfigEntriesWrite)
            .WithSummary("Create a configuration entry")
            .WithDescription("Creates a new configuration entry with a key, scoped values, and optional sensitivity flag.")
            .Produces<ConfigEntryResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(CreateConfigEntryHandler));
    }

    private async Task<IResult> HandleAsync(CreateConfigEntryRequest request, CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var plaintextValues = request.Values.Select(v => new ScopedValue(v.Value, v.Scopes));
        var protectedValues = _protector.ProtectValues(plaintextValues, request.IsSensitive);

        var entry = new ConfigEntry
        {
            Id = Guid.CreateVersion7(),
            Key = request.Key,
            OwnerId = request.OwnerId,
            OwnerType = request.OwnerType,
            ValueType = request.ValueType,
            Values = [.. protectedValues],
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

        var responseValues = SensitiveSourceValueProtector.MaskValues(entry.Values, entry.IsSensitive);
        return TypedResults.Created($"/api/config-entries/{entry.Id}", ConfigEntryResponse.From(entry, responseValues));
    }
}