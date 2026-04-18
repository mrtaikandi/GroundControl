using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GroundControl.Link;

/// <summary>
/// A configuration provider that loads configuration from a GroundControl server.
/// </summary>
internal sealed class GroundControlConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly IGroundControlApiClient _client;
    private readonly Lock _applyLock = new();
    private string? _appliedEtag;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroundControlConfigurationProvider"/> class.
    /// </summary>
    public GroundControlConfigurationProvider(GroundControlStore store, IConfigurationCache cache, IGroundControlApiClient client)
    {
        Store = store ?? throw new ArgumentNullException(nameof(store));
        Cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _client = client ?? throw new ArgumentNullException(nameof(client));

        Store.OnDataChanged += OnStoreDataChanged;
    }

    /// <summary>
    /// Gets the shared store, discovered via <c>IConfigurationRoot.Providers</c> traversal.
    /// </summary>
    internal GroundControlStore Store { get; }

    /// <summary>
    /// Gets the cache instance, discovered via <c>IConfigurationRoot.Providers</c> traversal.
    /// </summary>
    internal IConfigurationCache Cache { get; }

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
            var result = _client.FetchConfigAsync(etag, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
            switch (result.Status)
            {
                case FetchStatus.Success when result.Config is not null:
                    Store.Update(new Dictionary<string, ConfigValue>(result.Config, StringComparer.OrdinalIgnoreCase), result.ETag, null);
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

        SetDataFromStore(Store.GetSnapshot());
    }

    /// <inheritdoc />
    public void Dispose() => Store.OnDataChanged -= OnStoreDataChanged;

    private void OnStoreDataChanged()
    {
        // Gate on the snapshot's etag (server-assigned snapshot version) so that a replay
        // of the already-applied snapshot — e.g. the initial frame an SSE stream delivers
        // on connect, right after Load() just fetched the same snapshot via REST — does
        // not fire a spurious OnReload. Without this gate, a consumer that registered a
        // reload callback between Load() and the SSE replay arriving would be notified
        // with stale Data: the callback fires on the replay event, but Data still reflects
        // the REST-fetched snapshot which is identical to the replay, not any later change.
        lock (_applyLock)
        {
            var snapshot = Store.GetSnapshot();
            if (string.Equals(snapshot.ETag, _appliedEtag, StringComparison.Ordinal))
            {
                return;
            }

            _appliedEtag = snapshot.ETag;
            SetDataFromStore(snapshot);
        }

        OnReload();
    }

    private void SetDataFromStore(StoreSnapshot snapshot)
    {
        var data = new Dictionary<string, string?>(snapshot.Data.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in snapshot.Data)
        {
            data[key] = value.Value;
        }

        Data = data;
    }

    private void ApplyCache(CachedConfiguration? cache)
    {
        if (cache is null)
        {
            return;
        }

        Store.Update(new Dictionary<string, ConfigValue>(cache.Entries, StringComparer.OrdinalIgnoreCase), cache.ETag, cache.LastEventId);
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
    private void TrySaveToCache(IReadOnlyDictionary<string, ConfigValue> data, string? etag, string? lastEventId)
    {
        try
        {
            var entries = new Dictionary<string, ConfigValue>(data, StringComparer.OrdinalIgnoreCase);
            Cache.Save(new CachedConfiguration { Entries = entries, ETag = etag, LastEventId = lastEventId });
        }
        catch
        {
            // Best-effort
        }
    }
}