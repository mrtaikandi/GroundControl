using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace GroundControl.Link.Internals;

/// <summary>
/// A file-based <see cref="IConfigCache"/> implementation that persists configuration to disk
/// using atomic writes, with optional encryption of values via ASP.NET Data Protection.
/// </summary>
/// <remarks>
/// When a <see cref="IDataProtectionProvider"/> is supplied, all values are encrypted in the cache file.
/// The current <see cref="IConfigCache"/> interface does not carry per-key sensitivity metadata, so selective
/// encryption (encrypting only sensitive keys) will be added when the interface is extended with that information.
/// </remarks>
internal sealed class FileConfigCache : IConfigCache
{
    private const string EncryptedPrefix = "***ENCRYPTED:";
    private const string ProtectorPurpose = "GroundControl.Link.ConfigCache";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _cachePath;
    private readonly IDataProtector? _protector;
    private readonly ILogger<FileConfigCache> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileConfigCache"/> class.
    /// </summary>
    /// <param name="options">The SDK options containing the cache file path.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="dataProtection">Optional data protection provider for encrypting cached values.</param>
    public FileConfigCache(GroundControlOptions options, ILogger<FileConfigCache> logger, IDataProtectionProvider? dataProtection = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _cachePath = options.CacheFilePath;
        _logger = logger;
        _protector = dataProtection?.CreateProtector(ProtectorPurpose);

        if (_protector is null)
        {
            _logger.LogDataProtectionUnavailable();
        }
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
            if (value.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
            {
                if (_protector is null)
                {
                    _logger.LogCannotDecryptWithoutDataProtection();
                    return null;
                }

                var cipherText = value[EncryptedPrefix.Length..];
                result[key] = _protector.Unprotect(cipherText);
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
    [LoggerMessage(1, LogLevel.Information, "Data protection is not available. Cache values will be stored unencrypted.")]
    public static partial void LogDataProtectionUnavailable(this ILogger<FileConfigCache> logger);

    [LoggerMessage(2, LogLevel.Information, "Configuration loaded from local cache.")]
    public static partial void LogCacheLoaded(this ILogger<FileConfigCache> logger);

    [LoggerMessage(3, LogLevel.Warning, "Failed to read local cache file.")]
    public static partial void LogCacheReadFailed(this ILogger<FileConfigCache> logger, Exception exception);

    [LoggerMessage(4, LogLevel.Warning, "Cache contains encrypted values but data protection is not available. Treating as cache miss.")]
    public static partial void LogCannotDecryptWithoutDataProtection(this ILogger<FileConfigCache> logger);
}