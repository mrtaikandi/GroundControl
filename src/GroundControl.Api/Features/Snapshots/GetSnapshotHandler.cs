using GroundControl.Api.Features.Snapshots.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Snapshots;

internal sealed class GetSnapshotHandler : IEndpointHandler
{
    private const string SensitiveMask = "***";

    private readonly ISnapshotStore _snapshotStore;
    private readonly IValueProtector _protector;

    public GetSnapshotHandler(ISnapshotStore snapshotStore, IValueProtector protector)
    {
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
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

        var canDecrypt = decrypt && httpContext.User.HasClaim("permission", Permissions.SensitiveValuesDecrypt);

        var entries = snapshot.Entries.Select(entry => MapEntry(entry, canDecrypt)).ToList();

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
                Value = MaskIfSensitive(entry.IsSensitive, v.Value, canDecrypt),
            }).ToList()
        };
    }

    private string MaskIfSensitive(bool isSensitive, string value, bool canDecrypt)
    {
        if (!isSensitive)
        {
            return value;
        }

        return canDecrypt ? _protector.Unprotect(value) : SensitiveMask;
    }
}