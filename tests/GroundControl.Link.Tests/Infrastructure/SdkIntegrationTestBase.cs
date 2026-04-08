using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using GroundControl.Link.Internals;

namespace GroundControl.Link.Tests.Infrastructure;

/// <summary>
/// Provides shared helpers for SDK integration tests that exercise the SDK against a real API server.
/// </summary>
public abstract class SdkIntegrationTestBase
{
    private readonly MongoFixture _mongoFixture;

    protected SdkIntegrationTestBase(MongoFixture mongoFixture)
    {
        ArgumentNullException.ThrowIfNull(mongoFixture);
        _mongoFixture = mongoFixture;
    }

    protected static JsonSerializerOptions WebJsonSerializerOptions { get; } = new(JsonSerializerDefaults.Web);

    protected static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    protected GroundControlApiFactory CreateFactory(Dictionary<string, string?>? extraConfig = null) => new(_mongoFixture, extraConfig);

    /// <summary>
    /// Creates a Phase 1 provider wired to real SDK components using the test server handler.
    /// </summary>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Caller owns serverHttpClient; provider is disposed by test via using")]
    internal static GroundControlConfigurationProvider CreateSdkProvider(
        HttpMessageHandler serverHandler,
        Guid clientId,
        string clientSecret,
        GroundControlOptions? optionsOverride = null)
    {
        var options = optionsOverride ?? new GroundControlOptions
        {
            ServerUrl = new Uri("http://localhost"),
            ClientId = clientId.ToString(),
            ClientSecret = clientSecret,
            StartupTimeout = TimeSpan.FromSeconds(10),
            ConnectionMode = ConnectionMode.SseWithPollingFallback,
            PollingInterval = TimeSpan.FromHours(1),
            EnableLocalCache = false,
        };

        var store = new GroundControlStore(options);

        var httpClient = new HttpClient(serverHandler, disposeHandler: false) { BaseAddress = options.ServerUrl };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", $"{options.ClientId}:{options.ClientSecret}");
        httpClient.DefaultRequestHeaders.Add(HeaderNames.ApiVersion, options.ApiVersion);

        IConfigFetcher fetcher = new DefaultConfigFetcher(httpClient, NullLogger<DefaultConfigFetcher>.Instance);

        IConfigCache cache = options.EnableLocalCache
            ? new FileConfigCache(options, NullLogger<FileConfigCache>.Instance)
            : NullConfigCache.Instance;

        return new GroundControlConfigurationProvider(store, cache, fetcher);
    }

    /// <summary>
    /// Creates a provider and returns its store for wiring a background SSE/polling connection.
    /// </summary>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Caller owns serverHttpClient; provider is disposed by test via using")]
    internal static (GroundControlConfigurationProvider Provider, GroundControlStore Store, ISseClient SseClient) CreateProviderWithStore(
        HttpMessageHandler serverHandler,
        Guid clientId,
        string clientSecret,
        GroundControlOptions? optionsOverride = null)
    {
        var options = optionsOverride ?? new GroundControlOptions
        {
            ServerUrl = new Uri("http://localhost"),
            ClientId = clientId.ToString(),
            ClientSecret = clientSecret,
            StartupTimeout = TimeSpan.FromSeconds(10),
            ConnectionMode = ConnectionMode.SseWithPollingFallback,
            EnableLocalCache = false,
        };

        var store = new GroundControlStore(options);

        var httpClient = new HttpClient(serverHandler, disposeHandler: false) { BaseAddress = options.ServerUrl };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", $"{options.ClientId}:{options.ClientSecret}");
        httpClient.DefaultRequestHeaders.Add(HeaderNames.ApiVersion, options.ApiVersion);

        IConfigFetcher fetcher = new DefaultConfigFetcher(httpClient, NullLogger<DefaultConfigFetcher>.Instance);
        IConfigCache cache = NullConfigCache.Instance;
        ISseClient sseClient = new DefaultSseClient(httpClient, options, NullLogger<DefaultSseClient>.Instance);

        var provider = new GroundControlConfigurationProvider(store, cache, fetcher);

        return (provider, store, sseClient);
    }
}