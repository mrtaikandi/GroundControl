using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace GroundControl.Link;

/// <summary>
/// A file-based <see cref="IConfigCache"/> implementation that persists configuration to disk
/// using atomic writes, with optional encryption of values via ASP.NET Data Protection.
/// </summary>
/// <remarks>
/// When a <see cref="IDataProtectionProvider"/> is supplied, all values are encrypted in the cache file.
/// The current <see cref="IConfigCache"/> interface does not carry per-key sensitivity metadata, so selective
/// encryption (encrypting only sensitive keys) will be added when the interface is extended with that information.
/// </remarks>
public sealed partial class FileConfigCache : IConfigCache
{
    private const string EncryptedPrefix = "***ENCRYPTED:";
    private const string ProtectorPurpose = "GroundControl.Link.ConfigCache";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _cachePath;
    private readonly IDataProtector? _protector;
    private readonly ILogger<FileConfigCache> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileConfigCache"/> class.
    /// </summary>
    /// <param name="options">The SDK options containing the cache file path.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="dataProtection">Optional data protection provider for encrypting cached values.</param>
    public FileConfigCache(
        GroundControlOptions options,
        ILogger<FileConfigCache> logger,
        IDataProtectionProvider? dataProtection = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _cachePath = options.CacheFilePath;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _protector = dataProtection?.CreateProtector(ProtectorPurpose);

        if (_protector is null)
        {
            LogDataProtectionUnavailable(_logger);
        }
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Graceful degradation: corrupted or unreadable cache is treated as a cache miss")]
    public async Task<IReadOnlyDictionary<string, string>?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_cachePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_cachePath, cancellationToken).ConfigureAwait(false);
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
                        LogCannotDecryptWithoutDataProtection(_logger);
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

            LogCacheLoaded(_logger);
            return result;
        }
        catch (Exception ex)
        {
            LogCacheReadFailed(_logger, ex);
            return null;
        }
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best-effort temp file cleanup should not mask the original exception")]
    public async Task SaveAsync(IReadOnlyDictionary<string, string> config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var directory = Path.GetDirectoryName(_cachePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var entries = new Dictionary<string, string>(config.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in config)
        {
            entries[key] = _protector is not null
                ? EncryptedPrefix + _protector.Protect(value)
                : value;
        }

        var cacheFile = new CacheEnvelope
        {
            Timestamp = DateTimeOffset.UtcNow,
            Entries = entries
        };

        var json = JsonSerializer.Serialize(cacheFile, SerializerOptions);
        var tmpPath = _cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            await File.WriteAllTextAsync(tmpPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(tmpPath, _cachePath, overwrite: true);
        }
        finally
        {
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

    [LoggerMessage(1, LogLevel.Information, "Data protection is not available. Cache values will be stored unencrypted.")]
    private static partial void LogDataProtectionUnavailable(ILogger logger);

    [LoggerMessage(2, LogLevel.Information, "Configuration loaded from local cache.")]
    private static partial void LogCacheLoaded(ILogger logger);

    [LoggerMessage(3, LogLevel.Warning, "Failed to read local cache file.")]
    private static partial void LogCacheReadFailed(ILogger logger, Exception exception);

    [LoggerMessage(4, LogLevel.Warning, "Cache contains encrypted values but data protection is not available. Treating as cache miss.")]
    private static partial void LogCannotDecryptWithoutDataProtection(ILogger logger);

    internal sealed class CacheEnvelope
    {
        public Guid? SnapshotId { get; init; }

        public long? SnapshotVersion { get; init; }

        public DateTimeOffset Timestamp { get; init; }

        public Dictionary<string, string> Entries { get; init; } = [];
    }
}