using System.Diagnostics.CodeAnalysis;
using GroundControl.Link.Internals;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GroundControl.Link;

/// <summary>
/// A configuration provider that loads configuration from a GroundControl server.
/// </summary>
internal sealed class GroundControlConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly IConfigFetcher _fetcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroundControlConfigurationProvider"/> class.
    /// </summary>
    public GroundControlConfigurationProvider(GroundControlStore store, IConfigCache cache, IConfigFetcher fetcher)
    {
        Store = store ?? throw new ArgumentNullException(nameof(store));
        Cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));

        Store.OnDataChanged += OnStoreDataChanged;
    }

    /// <summary>
    /// Gets the shared store for Phase 2 extraction via <c>IConfigurationRoot.Providers</c> traversal.
    /// </summary>
    internal GroundControlStore Store { get; }

    /// <summary>
    /// Gets the cache instance for Phase 2 extraction.
    /// </summary>
    internal IConfigCache Cache { get; }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Startup must not crash the app. Degrade gracefully")]
    public override void Load()
    {
        CachedConfiguration? cached = null;

        try
        {
            cached = Cache.Load();
        }
        catch
        {
            // Cache read failure is non-fatal
        }

        var etag = cached?.ETag;

        try
        {
            var result = _fetcher.FetchAsync(etag, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
            switch (result.Status)
            {
                case FetchStatus.Success when result.Config is not null:
                    Store.Update(new Dictionary<string, string>(result.Config, StringComparer.OrdinalIgnoreCase), result.ETag, null);
                    TrySaveToCache(result.Config, result.ETag, null);
                    break;

                case FetchStatus.NotModified when cached is not null:
                    ApplyCache(cached);
                    break;

                case FetchStatus.TransientError:
                case FetchStatus.AuthenticationError:
                case FetchStatus.NotFound:
                default:
                    ApplyCache(cached);
                    MarkAsUnhealthy(cached, result.Status);
                    break;
            }
        }
        catch (Exception ex)
        {
            ApplyCache(cached);
            MarkAsUnhealthy(cached, error: ex);
        }

        SetDataFromStore();
    }

    /// <inheritdoc />
    public void Dispose() => Store.OnDataChanged -= OnStoreDataChanged;

    private void OnStoreDataChanged()
    {
        SetDataFromStore();
        OnReload();
    }

    private void SetDataFromStore()
    {
        var snapshot = Store.GetSnapshot();
        var data = new Dictionary<string, string?>(snapshot.Data.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in snapshot.Data)
        {
            data[key] = value;
        }

        Data = data;
    }

    private void ApplyCache(CachedConfiguration? cache)
    {
        if (cache is null)
        {
            return;
        }

        Store.Update(new Dictionary<string, string>(cache.Entries, StringComparer.OrdinalIgnoreCase), cache.ETag, cache.LastEventId);
    }

    private void MarkAsUnhealthy(CachedConfiguration? cached, FetchStatus? fetchStatus = null, Exception? error = null)
    {
        var reason = fetchStatus switch
        {
            FetchStatus.AuthenticationError => "Authentication failed (401/403). Check ClientId and ClientSecret.",
            FetchStatus.NotFound => "No active snapshot found on the server (404).",
            FetchStatus.TransientError => "Server returned a transient error.",
            _ when error is not null => error.Message,
            _ => null
        };

        Store.SetHealth(cached is not null ? HealthStatus.Degraded : HealthStatus.Unhealthy, reason, error);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Cache save is best-effort; failures are non-fatal")]
    private void TrySaveToCache(IReadOnlyDictionary<string, string> data, string? etag, string? lastEventId)
    {
        try
        {
            var entries = new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
            Cache.Save(new CachedConfiguration { Entries = entries, ETag = etag, LastEventId = lastEventId });
        }
        catch
        {
            // Best-effort
        }
    }
}