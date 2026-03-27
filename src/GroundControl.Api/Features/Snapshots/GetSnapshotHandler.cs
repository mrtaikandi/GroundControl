using GroundControl.Api.Features.Snapshots.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Masking;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Snapshots;

internal sealed class GetSnapshotHandler : IEndpointHandler
{
    private readonly ISnapshotStore _snapshotStore;
    private readonly SensitiveValueMasker _masker;
    private readonly AuditRecorder _audit;

    public GetSnapshotHandler(ISnapshotStore snapshotStore, SensitiveValueMasker masker, AuditRecorder audit)
    {
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _masker = masker ?? throw new ArgumentNullException(nameof(masker));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", async (
                Guid projectId,
                Guid id,
                HttpContext httpContext,
                [FromQuery] bool? decrypt,
                [FromServices] GetSnapshotHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(projectId, id, decrypt == true, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.SnapshotsRead)
            .WithName(nameof(GetSnapshotHandler));
    }

    private async Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        bool decrypt,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var snapshot = await _snapshotStore.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (snapshot is null || snapshot.ProjectId != projectId)
        {
            return TypedResults.Problem(
                detail: $"Snapshot '{id}' was not found for project '{projectId}'.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var canDecrypt = SensitiveValueMasker.CanDecrypt(httpContext, decrypt);
        var hasSensitive = canDecrypt && snapshot.Entries.Any(e => e.IsSensitive);

        var entries = snapshot.Entries.Select(entry => MapEntry(entry, canDecrypt)).ToList();

        if (hasSensitive)
        {
            await _audit.RecordAsync("Snapshot", snapshot.Id, null, "Decrypted", cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var response = new SnapshotResponse
        {
            Id = snapshot.Id,
            ProjectId = snapshot.ProjectId,
            SnapshotVersion = snapshot.SnapshotVersion,
            Entries = entries,
            PublishedAt = snapshot.PublishedAt,
            PublishedBy = snapshot.PublishedBy,
            Description = snapshot.Description,
        };

        return TypedResults.Ok(response);
    }

    private ResolvedEntryResponse MapEntry(ResolvedEntry entry, bool canDecrypt)
    {
        return new ResolvedEntryResponse
        {
            Key = entry.Key,
            ValueType = entry.ValueType,
            IsSensitive = entry.IsSensitive,
            Values = entry.Values.Select(v => new ScopedValueResponse
            {
                Scopes = v.Scopes,
                Value = _masker.MaskOrDecrypt(v.Value, entry.IsSensitive, canDecrypt),
            }).ToList()
        };
    }
}