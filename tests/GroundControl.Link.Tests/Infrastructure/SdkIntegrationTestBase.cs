using System.Diagnostics.CodeAnalysis;

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
    /// Creates a <see cref="GroundControlConfigurationProvider"/> wired to real SDK components
    /// pointing at the given test server.
    /// </summary>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Caller owns serverHttpClient; SSE client and provider are disposed by test via using")]
    protected static GroundControlConfigurationProvider CreateSdkProvider(
        HttpMessageHandler serverHandler,
        Guid clientId,
        string clientSecret,
        GroundControlOptions? optionsOverride = null)
    {
        var options = optionsOverride ?? new GroundControlOptions
        {
            ServerUrl = "http://localhost",
            ClientId = clientId.ToString(),
            ClientSecret = clientSecret,
            StartupTimeout = TimeSpan.FromSeconds(10),
            ConnectionMode = ConnectionMode.SseWithPollingFallback,
            PollingInterval = TimeSpan.FromHours(1),
            EnableLocalCache = false,
        };

        var authHandler = new GroundControlAuthHandler(options) { InnerHandler = serverHandler };
        var httpClient = new HttpClient(authHandler, disposeHandler: false)
        {
            BaseAddress = new Uri(options.ServerUrl)
        };

        var sseClient = new DefaultSseClient(httpClient, options, NullLogger<DefaultSseClient>.Instance);
        var configFetcher = new DefaultConfigFetcher(httpClient, options, NullLogger<DefaultConfigFetcher>.Instance);
        IConfigCache configCache = options.EnableLocalCache
            ? new FileConfigCache(options, NullLogger<FileConfigCache>.Instance)
            : NullConfigCache.Instance;

        return new GroundControlConfigurationProvider(
            options,
            sseClient,
            configFetcher,
            configCache,
            NullLogger<GroundControlConfigurationProvider>.Instance,
            httpClient);
    }
}