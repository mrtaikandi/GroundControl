using GroundControl.Api.Shared.Notification;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Http.HttpResults;
using MongoDB.Bson;

namespace GroundControl.Api.Features.Snapshots;

internal sealed class SnapshotPublisher
{
    private const int MaxBsonSizeBytes = 16_777_216; // 16MB
    private static readonly Dictionary<string, string> EmptyScopes = [];

    private readonly IProjectStore _projectStore;
    private readonly IConfigEntryStore _configEntryStore;
    private readonly IVariableStore _variableStore;
    private readonly VariableInterpolator _interpolator;
    private readonly IValueProtector _valueProtector;
    private readonly ISnapshotStore _snapshotStore;
    private readonly IChangeNotifier _changeNotifier;
    private readonly ILogger<SnapshotPublisher> _logger;

    public SnapshotPublisher(
        ILogger<SnapshotPublisher> logger,
        IProjectStore projectStore,
        IConfigEntryStore configEntryStore,
        IVariableStore variableStore,
        VariableInterpolator interpolator,
        IValueProtector valueProtector,
        ISnapshotStore snapshotStore,
        IChangeNotifier changeNotifier)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _projectStore = projectStore ?? throw new ArgumentNullException(nameof(projectStore));
        _configEntryStore = configEntryStore ?? throw new ArgumentNullException(nameof(configEntryStore));
        _variableStore = variableStore ?? throw new ArgumentNullException(nameof(variableStore));
        _interpolator = interpolator ?? throw new ArgumentNullException(nameof(interpolator));
        _valueProtector = valueProtector ?? throw new ArgumentNullException(nameof(valueProtector));
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _changeNotifier = changeNotifier ?? throw new ArgumentNullException(nameof(changeNotifier));
    }

    public async Task<Results<Created<Snapshot>, ProblemHttpResult, NotFound>> PublishAsync(
        Guid projectId,
        Guid publishedBy,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectStore.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return TypedResults.NotFound();
        }

        var (resolvedEntries, allUnresolved) = await ResolveAndInterpolateEntriesAsync(project, cancellationToken);
        if (allUnresolved.Count > 0)
        {
            return TypedResults.Problem(
                detail: $"Unresolved variable placeholders: {string.Join(", ", allUnresolved.Order())}",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        EncryptSensitiveValues(resolvedEntries);

        // Build snapshot and check BSON size
        var snapshotVersion = await _snapshotStore.GetNextVersionAsync(projectId, cancellationToken);
        var snapshot = new Snapshot
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            SnapshotVersion = snapshotVersion,
            Entries = resolvedEntries,
            PublishedAt = DateTimeOffset.UtcNow,
            PublishedBy = publishedBy,
            Description = description
        };

        var bsonBytes = snapshot.ToBsonDocument().ToBson();
        var bsonSize = bsonBytes.Length;
        if (bsonSize > MaxBsonSizeBytes)
        {
            return TypedResults.Problem(
                detail: $"Snapshot BSON size ({bsonSize:N0} bytes) exceeds the 16MB MongoDB document limit.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        await _snapshotStore.CreateAsync(snapshot, cancellationToken);

        var activated = await _projectStore.ActivateSnapshotAsync(projectId, snapshot.Id, project.Version, cancellationToken);
        if (!activated)
        {
            return TypedResults.Problem(
                detail: "The project was modified by another request. Please retry the publish operation.",
                statusCode: StatusCodes.Status409Conflict);
        }

        await _changeNotifier.NotifyAsync(projectId, snapshot.Id, cancellationToken);

        return TypedResults.Created($"/api/snapshots/{snapshot.Id}", snapshot);
    }

    private void EncryptSensitiveValues(List<ResolvedEntry> resolvedEntries)
    {
        foreach (var entry in resolvedEntries.Where(entry => entry.IsSensitive))
        {
            for (var i = 0; i < entry.Values.Count; i++)
            {
                var value = entry.Values[i];
                entry.Values[i] = entry.Values[i] with { Value = _valueProtector.Protect(value.Value) };
            }
        }
    }

    private async Task<ResolveResult> ResolveAndInterpolateEntriesAsync(Project project, CancellationToken cancellationToken)
    {
        var projectVariables = await _variableStore.GetProjectVariablesAsync(project.Id, cancellationToken);
        var projectVariablesDict = projectVariables.ToDictionary(v => v.Name, StringComparer.OrdinalIgnoreCase);

        var globalVariables = await _variableStore.GetGlobalVariablesForGroupAsync(project.GroupId, cancellationToken);
        var globalVariablesDict = globalVariables.ToDictionary(v => v.Name, StringComparer.OrdinalIgnoreCase);

        var mergedEntries = await CollectAndMergeEntriesAsync(project, cancellationToken);

        // Interpolate and validate variables
        var resolvedEntries = new List<ResolvedEntry>();
        var unresolvedPlaceholders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in mergedEntries)
        {
            var resolvedValues = new List<ScopedValue>();

            foreach (var scopedValue in entry.Values)
            {
                var result = _interpolator.Interpolate(scopedValue.Value, EmptyScopes, projectVariablesDict, globalVariablesDict);
                resolvedValues.Add(new ScopedValue(result.Value, scopedValue.Scopes));

                foreach (var unresolved in result.UnresolvedPlaceholders)
                {
                    unresolvedPlaceholders.Add(unresolved);
                }
            }

            resolvedEntries.Add(new ResolvedEntry
            {
                Key = entry.Key,
                ValueType = entry.ValueType,
                IsSensitive = entry.IsSensitive,
                Values = resolvedValues
            });
        }

        return new ResolveResult(resolvedEntries, unresolvedPlaceholders);
    }

    private async Task<List<ConfigEntry>> CollectAndMergeEntriesAsync(Project project, CancellationToken cancellationToken)
    {
        // Collect template entries in order (later templates override earlier ones)
        var entryMap = new Dictionary<string, ConfigEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var templateId in project.TemplateIds)
        {
            var templateEntries = await _configEntryStore.GetAllByOwnerAsync(templateId, ConfigEntryOwnerType.Template, cancellationToken);

            foreach (var entry in templateEntries)
            {
                entryMap[entry.Key] = entry;
            }
        }

        // Project entries override template entries
        var projectEntries = await _configEntryStore.GetAllByOwnerAsync(project.Id, ConfigEntryOwnerType.Project, cancellationToken);

        foreach (var entry in projectEntries)
        {
            entryMap[entry.Key] = entry;
        }

        return [.. entryMap.Values];
    }

    private readonly record struct ResolveResult(List<ResolvedEntry> ResolvedEntries, HashSet<string> UnresolvedPlaceholders);
}

internal static partial class SnapshotPublisherLogs
{
    [LoggerMessage(1, LogLevel.Error, "Failed to notify subscribers of snapshot change for project {ProjectId}.")]
    public static partial void LogNotificationFailed(this ILogger<SnapshotPublisher> logger, Exception exception, Guid projectId);
}