using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace GroundControl.Link.Internals.Cache;

/// <summary>
/// A file-based <see cref="IConfigurationCache"/> implementation that persists configuration to disk
/// using atomic writes, with selective encryption of sensitive values via a consumer-supplied <see cref="IConfigurationProtector"/>.
/// </summary>
/// <remarks>
/// Only entries whose <see cref="ConfigValue.IsSensitive" /> flag is set are passed through <see cref="IConfigurationProtector.Protect" />
/// when a protector is configured; non-sensitive entries are persisted as plaintext so diagnostics and alternate tooling can read them.
/// The envelope carries a <c>Protected</c> flag recording whether a protector was configured at write time; a mismatch with the current
/// protector state at read time is treated as a cache miss so the next save will atomically overwrite it.
/// </remarks>
internal sealed class FileConfigurationCache : IConfigurationCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _cachePath;
    private readonly IConfigurationProtector? _protector;
    private readonly ILogger<FileConfigurationCache> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileConfigurationCache"/> class.
    /// </summary>
    /// <param name="options">The SDK options containing the cache file path and optional protector.</param>
    /// <param name="logger">The logger instance.</param>
    public FileConfigurationCache(GroundControlOptions options, ILogger<FileConfigurationCache> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _cachePath = options.CacheFilePath;
        _logger = logger;
        _protector = options.Protector;
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Graceful degradation: corrupted or unreadable cache is treated as a cache miss")]
    public CachedConfiguration? Load()
    {
        if (!File.Exists(_cachePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_cachePath);
            return DeserializeEnvelope(json);
        }
        catch (Exception ex)
        {
            _logger.LogCacheReadFailed(ex);
            return null;
        }
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best-effort temp file cleanup should not mask the original exception")]
    public void Save(CachedConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var directory = Path.GetDirectoryName(_cachePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = SerializeEnvelope(config);
        var tmpPath = _cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        _writeLock.Wait();

        try
        {
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, _cachePath, overwrite: true);
        }
        finally
        {
            _writeLock.Release();

            if (File.Exists(tmpPath))
            {
                try
                {
                    File.Delete(tmpPath);
                }
                catch
                {
                    // Best-effort cleanup
                }
            }
        }
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Graceful degradation: corrupted or unreadable cache is treated as a cache miss")]
    public async Task<CachedConfiguration?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_cachePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_cachePath, cancellationToken).ConfigureAwait(false);
            return DeserializeEnvelope(json);
        }
        catch (Exception ex)
        {
            _logger.LogCacheReadFailed(ex);
            return null;
        }
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best-effort temp file cleanup should not mask the original exception")]
    public async Task SaveAsync(CachedConfiguration config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var directory = Path.GetDirectoryName(_cachePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = SerializeEnvelope(config);

        // Write to a temp file then atomically move to avoid leaving a corrupted
        // cache file if the process crashes mid-write.
        var tmpPath = _cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await File.WriteAllTextAsync(tmpPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(tmpPath, _cachePath, overwrite: true);
        }
        finally
        {
            _writeLock.Release();

            if (File.Exists(tmpPath))
            {
                try
                {
                    File.Delete(tmpPath);
                }
                catch
                {
                    // Best-effort cleanup
                }
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _writeLock.Dispose();

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Consumer-supplied Unprotect may throw any exception; treat all as a cache-miss signal")]
    private CachedConfiguration? DeserializeEnvelope(string json)
    {
        var cacheFile = JsonSerializer.Deserialize<CacheEnvelope>(json, SerializerOptions);

        if (cacheFile?.Entries is null)
        {
            return null;
        }

        var hasProtector = _protector is not null;
        if (cacheFile.Protected != hasProtector)
        {
            // The envelope was written under a different protection regime than we're currently configured for.
            // Serving either plaintext-as-ciphertext or ciphertext-as-plaintext would corrupt downstream readers,
            // so treat it as a cache miss and let the server refetch overwrite the file.
            _logger.LogCacheProtectionMismatch(cacheFile.Protected, hasProtector);
            return null;
        }

        var result = new Dictionary<string, ConfigValue>(cacheFile.Entries.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, entry) in cacheFile.Entries)
        {
            string value;

            if (entry.IsSensitive && _protector is not null)
            {
                try
                {
                    value = _protector.Unprotect(entry.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogCacheDecryptFailed(ex);
                    return null;
                }
            }
            else
            {
                value = entry.Value;
            }

            result[key] = new ConfigValue { Value = value, IsSensitive = entry.IsSensitive };
        }

        _logger.LogCacheLoaded();
        return new CachedConfiguration
        {
            Entries = result,
            ETag = cacheFile.ETag,
            LastEventId = cacheFile.LastEventId,
        };
    }

    private string SerializeEnvelope(CachedConfiguration config)
    {
        var entries = new Dictionary<string, CachedEntry>(config.Entries.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, entry) in config.Entries)
        {
            var storedValue = entry.IsSensitive && _protector is not null ? _protector.Protect(entry.Value) : entry.Value;
            entries[key] = new CachedEntry { Value = storedValue, IsSensitive = entry.IsSensitive };
        }

        var cacheFile = new CacheEnvelope
        {
            Timestamp = DateTimeOffset.UtcNow,
            Protected = _protector is not null,
            ETag = config.ETag,
            LastEventId = config.LastEventId,
            Entries = entries
        };

        return JsonSerializer.Serialize(cacheFile, SerializerOptions);
    }

    internal sealed class CacheEnvelope
    {
        public string? ETag { get; init; }

        public string? LastEventId { get; init; }

        public DateTimeOffset Timestamp { get; init; }

        public bool Protected { get; init; }

        public Dictionary<string, CachedEntry> Entries { get; init; } = [];
    }

    internal sealed class CachedEntry
    {
        public string Value { get; init; } = string.Empty;

        public bool IsSensitive { get; init; }
    }
}

internal static partial class FileConfigCacheLogs
{
    [LoggerMessage(1, LogLevel.Information, "Configuration loaded from local cache.")]
    public static partial void LogCacheLoaded(this ILogger<FileConfigurationCache> logger);

    [LoggerMessage(2, LogLevel.Warning, "Failed to read local cache file.")]
    public static partial void LogCacheReadFailed(this ILogger<FileConfigurationCache> logger, Exception exception);

    [LoggerMessage(3, LogLevel.Warning, "Cache envelope protection state ({EnvelopeProtected}) does not match the current protector configuration ({ProtectorConfigured}); treating as cache miss.")]
    public static partial void LogCacheProtectionMismatch(this ILogger<FileConfigurationCache> logger, bool envelopeProtected, bool protectorConfigured);

    [LoggerMessage(4, LogLevel.Warning, "Failed to decrypt a cached value; treating as cache miss.")]
    public static partial void LogCacheDecryptFailed(this ILogger<FileConfigurationCache> logger, Exception exception);
}