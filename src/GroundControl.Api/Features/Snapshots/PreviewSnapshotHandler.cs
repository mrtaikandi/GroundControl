using GroundControl.Api.Features.Snapshots.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Snapshots;

internal sealed class PreviewSnapshotHandler : IEndpointHandler
{
    private readonly IProjectStore _projectStore;
    private readonly SnapshotResolver _resolver;
    private readonly SensitiveValueMasker _masker;
    private readonly AuditRecorder _audit;

    public PreviewSnapshotHandler(IProjectStore projectStore, SnapshotResolver resolver, SensitiveValueMasker masker, AuditRecorder audit)
    {
        _projectStore = projectStore ?? throw new ArgumentNullException(nameof(projectStore));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _masker = masker ?? throw new ArgumentNullException(nameof(masker));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/preview", async (
                Guid projectId,
                HttpContext httpContext,
                [FromQuery] bool? decrypt,
                [FromServices] PreviewSnapshotHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(projectId, decrypt == true, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.SnapshotsPublish)
            .WithSummary("Preview a snapshot")
            .WithDescription("Resolves the project's current configuration into a snapshot-shaped payload without persisting it. Returns a diff hash that publish will use to detect drift.")
            .Produces<PreviewSnapshotResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithName(nameof(PreviewSnapshotHandler));
    }

    private async Task<IResult> HandleAsync(Guid projectId, bool decryptRequested, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var project = await _projectStore.GetByIdAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return TypedResults.Problem(
                detail: $"Project '{projectId}' was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var resolved = await _resolver.ResolveAsync(project, description: null, cancellationToken).ConfigureAwait(false);

        if (resolved.UnresolvedPlaceholders.Count > 0)
        {
            return TypedResults.Problem(
                detail: $"Unresolved variable placeholders: {string.Join(", ", resolved.UnresolvedPlaceholders.Order())}",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        if (resolved.BsonSizeBytes > SnapshotResolver.MaxBsonSizeBytes)
        {
            return TypedResults.Problem(
                detail: $"Snapshot BSON size ({resolved.BsonSizeBytes:N0} bytes) exceeds the 16MB MongoDB document limit.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var canDecrypt = SensitiveValueMasker.CanDecrypt(httpContext, decryptRequested);
        var hasSensitive = canDecrypt && resolved.EncryptedEntries.Any(e => e.IsSensitive);

        var entries = resolved.EncryptedEntries.Select(entry => MapEntry(entry, canDecrypt)).ToList();

        if (hasSensitive)
        {
            await _audit.RecordAsync("Snapshot", projectId, project.GroupId, "Decrypted", cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return TypedResults.Ok(new PreviewSnapshotResponse
        {
            ProjectId = projectId,
            NextVersion = resolved.NextVersion,
            BsonSizeBytes = resolved.BsonSizeBytes,
            DiffHash = resolved.DiffHash,
            Entries = entries,
        });
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
            }).ToList(),
        };
    }
}