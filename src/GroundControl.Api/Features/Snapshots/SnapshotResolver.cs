using System.Security.Cryptography;
using System.Text.Json;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using MongoDB.Bson;

namespace GroundControl.Api.Features.Snapshots;

internal sealed class SnapshotResolver
{
    public const int MaxBsonSizeBytes = 16_777_216;
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new() { WriteIndented = false };

    private readonly IConfigEntryStore _configEntryStore;
    private readonly IVariableStore _variableStore;
    private readonly ResolvedEntryBuilder _resolvedEntryBuilder;
    private readonly SensitiveSourceValueProtector _sourceProtector;
    private readonly ISnapshotStore _snapshotStore;

    public SnapshotResolver(
        IConfigEntryStore configEntryStore,
        IVariableStore variableStore,
        ResolvedEntryBuilder resolvedEntryBuilder,
        SensitiveSourceValueProtector sourceProtector,
        ISnapshotStore snapshotStore)
    {
        _configEntryStore = configEntryStore ?? throw new ArgumentNullException(nameof(configEntryStore));
        _variableStore = variableStore ?? throw new ArgumentNullException(nameof(variableStore));
        _resolvedEntryBuilder = resolvedEntryBuilder ?? throw new ArgumentNullException(nameof(resolvedEntryBuilder));
        _sourceProtector = sourceProtector ?? throw new ArgumentNullException(nameof(sourceProtector));
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
    }

    /// <summary>
    /// Resolves the project's effective configuration into a snapshot-shaped payload that mirrors what
    /// would be persisted by a real publish call. Returns both plaintext entries (for hashing and for
    /// callers that want to mask at the response boundary) and storage-encrypted entries (for callers
    /// that intend to persist).
    /// </summary>
    public async Task<SnapshotResolveResult> ResolveAsync(Project project, string? description, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var plaintextEntries = await ResolveAndInterpolateAsync(project, cancellationToken).ConfigureAwait(false);
        var encryptedEntries = EncryptForStorage(plaintextEntries.Entries);
        var nextVersion = await _snapshotStore.GetNextVersionAsync(project.Id, cancellationToken).ConfigureAwait(false);
        var bsonSizeBytes = MeasureBsonSize(project.Id, nextVersion, description, encryptedEntries);
        var diffHash = ComputeDiffHash(plaintextEntries.Entries);

        return new SnapshotResolveResult
        {
            PlaintextEntries = plaintextEntries.Entries,
            EncryptedEntries = encryptedEntries,
            NextVersion = nextVersion,
            BsonSizeBytes = bsonSizeBytes,
            DiffHash = diffHash,
            UnresolvedPlaceholders = plaintextEntries.UnresolvedPlaceholders,
        };
    }

    private async Task<(IReadOnlyList<ResolvedEntry> Entries, IReadOnlySet<string> UnresolvedPlaceholders)> ResolveAndInterpolateAsync(Project project, CancellationToken cancellationToken)
    {
        var projectVariables = await _variableStore.GetProjectVariablesAsync(project.Id, cancellationToken).ConfigureAwait(false);
        var projectVariablesDict = BuildPlaintextVariableLookup(projectVariables);

        var globalVariables = await _variableStore.GetGlobalVariablesForGroupAsync(project.GroupId, cancellationToken).ConfigureAwait(false);
        var globalVariablesDict = BuildPlaintextVariableLookup(globalVariables);

        var mergedEntries = await CollectAndMergeEntriesAsync(project, cancellationToken).ConfigureAwait(false);

        var resolvedEntries = new List<ResolvedEntry>(mergedEntries.Count);
        var unresolvedPlaceholders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in mergedEntries)
        {
            var plaintextValues = _sourceProtector.UnprotectValues(entry.Values, entry.IsSensitive);
            var buildResult = _resolvedEntryBuilder.Build(plaintextValues, projectVariablesDict, globalVariablesDict);

            foreach (var name in buildResult.UnresolvedPlaceholders)
            {
                unresolvedPlaceholders.Add(name);
            }

            resolvedEntries.Add(new ResolvedEntry
            {
                Key = entry.Key,
                ValueType = entry.ValueType,
                IsSensitive = entry.IsSensitive || buildResult.UsedSensitiveVariable,
                Values = buildResult.Values,
            });
        }

        return (resolvedEntries, unresolvedPlaceholders);
    }

    private async Task<List<ConfigEntry>> CollectAndMergeEntriesAsync(Project project, CancellationToken cancellationToken)
    {
        var entryMap = new Dictionary<string, ConfigEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var templateId in project.TemplateIds)
        {
            var templateEntries = await _configEntryStore.GetAllByOwnerAsync(templateId, ConfigEntryOwnerType.Template, cancellationToken).ConfigureAwait(false);

            foreach (var entry in templateEntries)
            {
                entryMap[entry.Key] = entry;
            }
        }

        var projectEntries = await _configEntryStore.GetAllByOwnerAsync(project.Id, ConfigEntryOwnerType.Project, cancellationToken).ConfigureAwait(false);

        foreach (var entry in projectEntries)
        {
            entryMap[entry.Key] = entry;
        }

        return [.. entryMap.Values];
    }

    private Dictionary<string, PlaintextVariable> BuildPlaintextVariableLookup(IReadOnlyList<Variable> variables)
    {
        var lookup = new Dictionary<string, PlaintextVariable>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in variables)
        {
            var plaintextValues = _sourceProtector.UnprotectValues(variable.Values, variable.IsSensitive);
            lookup[variable.Name] = new PlaintextVariable
            {
                Values = plaintextValues,
                IsSensitive = variable.IsSensitive,
            };
        }

        return lookup;
    }

    private List<ResolvedEntry> EncryptForStorage(IReadOnlyList<ResolvedEntry> plaintextEntries)
    {
        var encrypted = new List<ResolvedEntry>(plaintextEntries.Count);

        foreach (var entry in plaintextEntries)
        {
            if (!entry.IsSensitive)
            {
                encrypted.Add(new ResolvedEntry
                {
                    Key = entry.Key,
                    ValueType = entry.ValueType,
                    IsSensitive = entry.IsSensitive,
                    Values = [.. entry.Values],
                });
                continue;
            }

            var protectedValues = _sourceProtector.ProtectValues(entry.Values, isSensitive: true);
            encrypted.Add(new ResolvedEntry
            {
                Key = entry.Key,
                ValueType = entry.ValueType,
                IsSensitive = entry.IsSensitive,
                Values = [.. protectedValues],
            });
        }

        return encrypted;
    }

    private static long MeasureBsonSize(Guid projectId, long snapshotVersion, string? description, IReadOnlyList<ResolvedEntry> encryptedEntries)
    {
        var probe = new Snapshot
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            SnapshotVersion = snapshotVersion,
            Entries = [.. encryptedEntries],
            PublishedAt = DateTimeOffset.UtcNow,
            PublishedBy = Guid.Empty,
            Description = description,
        };

        return probe.ToBsonDocument().ToBson().Length;
    }

    private static string ComputeDiffHash(IReadOnlyList<ResolvedEntry> plaintextEntries)
    {
        var canonical = plaintextEntries
            .OrderBy(e => e.Key, StringComparer.Ordinal)
            .Select(entry => new
            {
                key = entry.Key,
                valueType = entry.ValueType,
                isSensitive = entry.IsSensitive,
                values = entry.Values
                    .Select(value => new
                    {
                        scopes = value.Scopes
                            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                            .Select(pair => new { dim = pair.Key, val = pair.Value })
                            .ToList(),
                        value = value.Value,
                    })
                    .OrderBy(v => SerializeScopes(v.scopes), StringComparer.Ordinal)
                    .ThenBy(v => v.value, StringComparer.Ordinal)
                    .ToList(),
            })
            .ToList();

        var bytes = JsonSerializer.SerializeToUtf8Bytes(canonical, CanonicalJsonOptions);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string SerializeScopes<T>(IEnumerable<T> scopes) => string.Concat(scopes.Select(scope => scope?.ToString() ?? string.Empty));
}

/// <summary>
/// Result of resolving a project's configuration into a snapshot-shaped payload.
/// </summary>
internal sealed record SnapshotResolveResult
{
    /// <summary>
    /// Gets the resolved entries with sensitive values left as plaintext. Used for hashing and for
    /// response paths that mask via <see cref="SensitiveValueMasker.MaskOrDecrypt"/> against plaintext.
    /// </summary>
    public required IReadOnlyList<ResolvedEntry> PlaintextEntries { get; init; }

    /// <summary>
    /// Gets the resolved entries with sensitive values encrypted via <see cref="SensitiveSourceValueProtector"/>.
    /// Suitable for direct persistence as <see cref="Snapshot.Entries"/>.
    /// </summary>
    public required IReadOnlyList<ResolvedEntry> EncryptedEntries { get; init; }

    /// <summary>
    /// Gets the version that would be assigned if a snapshot were created right now. Allocated from
    /// the snapshot store's monotonic sequence; reading this value does not consume a slot.
    /// </summary>
    public required long NextVersion { get; init; }

    /// <summary>
    /// Gets the BSON document size of the would-be snapshot in bytes, computed against the encrypted
    /// payload. Used to fail-fast against MongoDB's 16MB document limit.
    /// </summary>
    public required long BsonSizeBytes { get; init; }

    /// <summary>
    /// Gets a deterministic SHA-256 hex digest over the plaintext entries, used to detect drift
    /// between a preview and a subsequent publish call.
    /// </summary>
    public required string DiffHash { get; init; }

    /// <summary>
    /// Gets the set of variable placeholders that could not be resolved against the project's or
    /// group's variables.
    /// </summary>
    public required IReadOnlySet<string> UnresolvedPlaceholders { get; init; }

    /// <summary>
    /// Gets a value indicating whether the resolved payload is publishable (no unresolved placeholders
    /// and within the BSON size limit).
    /// </summary>
    public bool IsPublishable => UnresolvedPlaceholders.Count == 0 && BsonSizeBytes <= SnapshotResolver.MaxBsonSizeBytes;
}