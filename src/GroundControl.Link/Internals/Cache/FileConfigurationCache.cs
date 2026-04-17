using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace GroundControl.Link.Internals.Cache;

/// <summary>
/// A file-based <see cref="IConfigurationCache"/> implementation that persists configuration to disk
/// using atomic writes, with optional encryption of values via a consumer-supplied <see cref="IConfigurationProtector"/>.
/// </summary>
/// <remarks>
/// When an <see cref="IConfigurationProtector"/> is supplied, every value in the cache file is encrypted
/// and prefixed with <c>***ENCRYPTED:</c>. The marker lets the reader detect a mismatched configuration
/// (cache written with a protector but read without one, or vice versa) and treat the file as a cache miss;
/// the next <see cref="SaveAsync"/> atomically overwrites it.
/// </remarks>
internal sealed class FileConfigurationCache : IConfigurationCache
{
    private const string EncryptedPrefix = "***ENCRYPTED:";

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

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Consumer-supplied Unprotect may throw any exception; treat all as a cache-miss signal")]
    private CachedConfiguration? DeserializeEnvelope(string json)
    {
        var cacheFile = JsonSerializer.Deserialize<CacheEnvelope>(json, SerializerOptions);

        if (cacheFile?.Entries is null)
        {
            return null;
        }

        var result = new Dictionary<string, string>(cacheFile.Entries.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in cacheFile.Entries)
        {
            var isEncrypted = value.StartsWith(EncryptedPrefix, StringComparison.Ordinal);

            if (isEncrypted && _protector is null)
            {
                _logger.LogCacheProtectorMissing();
                return null;
            }

            if (!isEncrypted && _protector is not null)
            {
                _logger.LogCacheUnexpectedPlaintext();
                return null;
            }

            if (isEncrypted)
            {
                var cipherText = value[EncryptedPrefix.Length..];
                try
                {
                    result[key] = _protector!.Unprotect(cipherText);
                }
                catch (Exception ex)
                {
                    _logger.LogCacheDecryptFailed(ex);
                    return null;
                }
            }
            else
            {
                result[key] = value;
            }
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
        var entries = new Dictionary<string, string>(config.Entries.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in config.Entries)
        {
            entries[key] = _protector is not null
                ? EncryptedPrefix + _protector.Protect(value)
                : value;
        }

        var cacheFile = new CacheEnvelope
        {
            Timestamp = DateTimeOffset.UtcNow,
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

        public Dictionary<string, string> Entries { get; init; } = [];
    }
}

internal static partial class FileConfigCacheLogs
{
    [LoggerMessage(1, LogLevel.Information, "Configuration loaded from local cache.")]
    public static partial void LogCacheLoaded(this ILogger<FileConfigurationCache> logger);

    [LoggerMessage(2, LogLevel.Warning, "Failed to read local cache file.")]
    public static partial void LogCacheReadFailed(this ILogger<FileConfigurationCache> logger, Exception exception);

    [LoggerMessage(3, LogLevel.Warning, "Cache contains encrypted values but no IConfigurationProtector is configured; treating as cache miss.")]
    public static partial void LogCacheProtectorMissing(this ILogger<FileConfigurationCache> logger);

    [LoggerMessage(4, LogLevel.Warning, "Cache contains unprotected values but an IConfigurationProtector is configured; treating as cache miss to prevent a silent downgrade.")]
    public static partial void LogCacheUnexpectedPlaintext(this ILogger<FileConfigurationCache> logger);

    [LoggerMessage(5, LogLevel.Warning, "Failed to decrypt a cached value; treating as cache miss.")]
    public static partial void LogCacheDecryptFailed(this ILogger<FileConfigurationCache> logger, Exception exception);
}