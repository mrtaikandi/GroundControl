using System.Diagnostics.CodeAnalysis;
using GroundControl.Link.Internals;

namespace GroundControl.Link;

/// <summary>
/// A configuration provider that loads configuration from a GroundControl server.
/// Phase 1: Synchronous cache read + conditional REST fetch during <see cref="Load"/>.
/// Phase 2: Background service pushes live updates via <see cref="GroundControlStore.OnDataChanged"/>.
/// </summary>
internal sealed class GroundControlConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly IConfigCache _cache;
    private readonly IConfigFetcher _fetcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroundControlConfigurationProvider"/> class.
    /// </summary>
    public GroundControlConfigurationProvider(
        GroundControlStore store,
        IConfigCache cache,
        IConfigFetcher fetcher)
    {
        Store = store ?? throw new ArgumentNullException(nameof(store));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
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
    internal IConfigCache Cache => _cache;

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Phase 1 startup must not crash the app — degrade gracefully")]
    public override void Load()
    {
        // Step 1: Synchronous cache read
        CachedConfiguration? cached = null;
        try
        {
            cached = _cache.Load();
        }
        catch
        {
            // Cache read failure is non-fatal
        }

        var etag = cached?.ETag;

        // Step 2: Conditional GET (sync-over-async — unavoidable per IConfigurationProvider contract)
        try
        {
            var result = _fetcher.FetchAsync(etag, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();

            switch (result.Status)
            {
                case FetchStatus.Success when result.Config is not null:
                    Store.Update(
                        new Dictionary<string, string>(result.Config, StringComparer.OrdinalIgnoreCase),
                        result.ETag,
                        null);
                    TrySaveToCache(result.Config, result.ETag, null);
                    break;

                case FetchStatus.NotModified when cached is not null:
                    Store.Update(
                        new Dictionary<string, string>(cached.Entries, StringComparer.OrdinalIgnoreCase),
                        cached.ETag,
                        cached.LastEventId);
                    break;

                default:
                    ApplyCacheOrMarkUnhealthy(cached);
                    break;
            }
        }
        catch
        {
            ApplyCacheOrMarkUnhealthy(cached);
        }

        // Step 3: Populate ConfigurationProvider.Data from store snapshot
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

    private void ApplyCacheOrMarkUnhealthy(CachedConfiguration? cached)
    {
        if (cached is not null)
        {
            Store.Update(
                new Dictionary<string, string>(cached.Entries, StringComparer.OrdinalIgnoreCase),
                cached.ETag,
                cached.LastEventId);
            Store.SetHealth(StoreHealthStatus.Degraded);
        }
        else
        {
            Store.SetHealth(StoreHealthStatus.Unhealthy);
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Cache save is best-effort; failures are non-fatal")]
    private void TrySaveToCache(IReadOnlyDictionary<string, string> data, string? etag, string? lastEventId)
    {
        try
        {
            var entries = new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
            _cache.Save(new CachedConfiguration { Entries = entries, ETag = etag, LastEventId = lastEventId });
        }
        catch
        {
            // Best-effort
        }
    }
}