using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text.Json;
using GroundControl.Api.Client;
using GroundControl.Link;
using Microsoft.Extensions.Logging.Abstractions;

namespace GroundControl.E2E.Tests.Infrastructure;

/// <summary>
/// Base class for E2E scenario tests. Provides CLI runner, API client, and Link SDK helpers.
/// </summary>
[TestCaseOrderer(typeof(StepOrderer))]
public abstract class E2ETestBase : IDisposable
{
    /// <summary>
    /// Tracks which steps have failed per scenario class, enabling skip-on-failure.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<int, bool>> FailedSteps = new();

    /// <summary>
    /// Shared state between ordered test steps, keyed by scenario class type.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, object>> SharedState = new();

    private readonly HttpClient _apiHttpClient;
    private bool _disposed;

    protected E2ETestBase(DockerComposeFixture fixture)
    {
        Fixture = fixture;

        _apiHttpClient = new HttpClient { BaseAddress = new Uri(fixture.ApiBaseUrl) };
        ApiClient = new GroundControlClient(_apiHttpClient);
        Cli = new CliRunner(fixture.ApiBaseUrl);
    }

    protected DockerComposeFixture Fixture { get; }

    protected CliRunner Cli { get; }

    protected GroundControlClient ApiClient { get; }

    protected static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    protected static JsonSerializerOptions WebJsonSerializerOptions { get; } = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Creates a Link SDK configuration provider wired to the fixture's API server.
    /// The caller owns the returned provider and must dispose it.
    /// </summary>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Ownership transfers to the returned GroundControlConfigurationProvider")]
    internal GroundControlConfigurationProvider CreateLinkProvider(Guid clientId, string clientSecret)
    {
        var httpClient = new HttpClient { BaseAddress = new Uri(Fixture.ApiBaseUrl) };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", $"{clientId}:{clientSecret}");
        httpClient.DefaultRequestHeaders.Add(HeaderNames.ApiVersion, "1.0");

        var options = new GroundControlOptions
        {
            ServerUrl = new Uri(Fixture.ApiBaseUrl),
            ClientId = clientId.ToString(),
            ClientSecret = clientSecret,
            StartupTimeout = TimeSpan.FromSeconds(15),
            ConnectionMode = ConnectionMode.Polling,
            PollingInterval = TimeSpan.FromHours(1),
            EnableLocalCache = false,
        };

        var store = new GroundControlStore(options);
        var apiClient = new GroundControlApiClient(httpClient, NullLogger<GroundControlApiClient>.Instance);

        return new GroundControlConfigurationProvider(store, NullConfigurationCache.Instance, apiClient);
    }

    /// <summary>
    /// Stores a value in shared state for the current scenario class.
    /// </summary>
    protected void Set<T>(string key, T value) where T : notnull
    {
        var state = SharedState.GetOrAdd(GetType(), _ => new ConcurrentDictionary<string, object>());
        state[key] = value;
    }

    /// <summary>
    /// Retrieves a value from shared state for the current scenario class.
    /// </summary>
    protected T Get<T>(string key)
    {
        var state = SharedState.GetOrAdd(GetType(), _ => new ConcurrentDictionary<string, object>());
        if (!state.TryGetValue(key, out var value))
        {
            throw new KeyNotFoundException($"Shared state key '{key}' not found. Ensure a prior step stored it.");
        }

        return (T)value;
    }

    /// <summary>
    /// Wraps step execution with automatic skip-on-failure tracking.
    /// Skips if a prior step failed; marks the current step as failed on exception.
    /// </summary>
    protected async Task RunStep(int step, Func<Task> body)
    {
        SkipIfPriorStepFailed(step);

        try
        {
            await body().ConfigureAwait(false);
        }
        catch
        {
            MarkStepFailed(step);
            throw;
        }
    }

    /// <summary>
    /// Records that a step has failed so subsequent steps can be skipped.
    /// </summary>
    private void MarkStepFailed(int step)
    {
        var failures = FailedSteps.GetOrAdd(GetType(), _ => new ConcurrentDictionary<int, bool>());
        failures[step] = true;
    }

    /// <summary>
    /// Skips the current test if any prior step (with a lower order) has failed.
    /// </summary>
    private void SkipIfPriorStepFailed(int currentStep)
    {
        var failures = FailedSteps.GetOrAdd(GetType(), _ => new ConcurrentDictionary<int, bool>());
        var hasPriorFailure = failures.Keys.Any(failedStep => failedStep < currentStep);

        Assert.SkipWhen(hasPriorFailure, $"Skipped because a prior step (before step {currentStep}) failed.");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _apiHttpClient.Dispose();
        }

        _disposed = true;
    }
}