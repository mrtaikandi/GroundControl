using GroundControl.Api.Core.ChangeNotification;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Http.HttpResults;

namespace GroundControl.Api.Features.Snapshots;

internal sealed class SnapshotPublisher
{
    private const int DefaultRetentionCount = 50;

    private readonly IProjectStore _projectStore;
    private readonly ISnapshotStore _snapshotStore;
    private readonly SnapshotResolver _resolver;
    private readonly IChangeNotifier _changeNotifier;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SnapshotPublisher> _logger;

    public SnapshotPublisher(
        ILogger<SnapshotPublisher> logger,
        IProjectStore projectStore,
        ISnapshotStore snapshotStore,
        SnapshotResolver resolver,
        IChangeNotifier changeNotifier,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _projectStore = projectStore ?? throw new ArgumentNullException(nameof(projectStore));
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _changeNotifier = changeNotifier ?? throw new ArgumentNullException(nameof(changeNotifier));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<Results<Created<Snapshot>, ProblemHttpResult, NotFound>> PublishAsync(
        Guid projectId,
        Guid publishedBy,
        string? description = null,
        string? expectedHash = null,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectStore.GetByIdAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return TypedResults.NotFound();
        }

        var resolved = await _resolver.ResolveAsync(project, description, cancellationToken).ConfigureAwait(false);

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

        if (expectedHash is not null && !string.Equals(expectedHash, resolved.DiffHash, StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.Problem(
                detail: "Configuration changed since the preview was generated. Refresh the diff and try again.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var snapshot = new Snapshot
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            SnapshotVersion = resolved.NextVersion,
            Entries = [.. resolved.EncryptedEntries],
            PublishedAt = DateTimeOffset.UtcNow,
            PublishedBy = publishedBy,
            Description = description,
        };

        await _snapshotStore.CreateAsync(snapshot, cancellationToken).ConfigureAwait(false);

        var activated = await _projectStore.ActivateSnapshotAsync(projectId, snapshot.Id, project.Version, cancellationToken).ConfigureAwait(false);
        if (!activated)
        {
            return TypedResults.Problem(
                detail: "The project was modified by another request. Please retry the publish operation.",
                statusCode: StatusCodes.Status409Conflict);
        }

        await _changeNotifier.NotifyAsync(projectId, snapshot.Id, cancellationToken).ConfigureAwait(false);

        try
        {
            var retentionCount = _configuration.GetValue("Snapshots:RetentionCount", DefaultRetentionCount);
            if (retentionCount > 0)
            {
                await _snapshotStore.DeleteOldSnapshotsAsync(projectId, retentionCount, snapshot.Id, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogRetentionCleanupFailed(ex, projectId);
        }

        return TypedResults.Created($"/api/snapshots/{snapshot.Id}", snapshot);
    }
}

internal static partial class SnapshotPublisherLogs
{
    [LoggerMessage(1, LogLevel.Error, "Failed to notify subscribers of snapshot change for project {ProjectId}.")]
    public static partial void LogNotificationFailed(this ILogger<SnapshotPublisher> logger, Exception exception, Guid projectId);

    [LoggerMessage(2, LogLevel.Error, "Snapshot retention cleanup failed for project {ProjectId}.")]
    public static partial void LogRetentionCleanupFailed(this ILogger<SnapshotPublisher> logger, Exception exception, Guid projectId);
}