using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.Testing;

namespace GroundControl.E2E.Tests.Infrastructure;

/// <summary>
/// Assembly-level fixture that manages the Aspire distributed application lifecycle for E2E tests.
/// Starts the AppHost in InitializeAsync, tears it down in DisposeAsync.
/// </summary>
public sealed class AspireFixture : IAsyncLifetime
{
    private const int HealthCheckTimeoutSeconds = 120;
    private const int HealthCheckPollIntervalMs = 2000;

    private DistributedApplicationFactory? _factory;

    /// <summary>
    /// Gets the base URL of the API server (e.g., http://127.0.0.1:54321).
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "String URL is the design contract for consumer convenience")]
    public string ApiBaseUrl { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await CliRunner.BuildAsync().ConfigureAwait(false);

        _factory = new DistributedApplicationFactory(typeof(Projects.GroundControl_E2E_Tests_AppHost));
        await _factory.StartAsync().ConfigureAwait(false);

        // Use 127.0.0.1 instead of localhost to avoid IPv6 resolution issues.
        // Docker maps ports to 0.0.0.0 (IPv4 only), but localhost may resolve
        // to ::1 (IPv6) first on Windows, causing .NET's HttpClient to hang.
        var endpoint = _factory.GetEndpoint("api");
        var uriBuilder = new UriBuilder(endpoint) { Host = "127.0.0.1" };
        ApiBaseUrl = uriBuilder.Uri.GetLeftPart(UriPartial.Authority);

        await WaitForHealthAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task WaitForHealthAsync()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri(ApiBaseUrl) };
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(HealthCheckTimeoutSeconds));

        try
        {
            while (true)
            {
                try
                {
                    var response = await httpClient.GetAsync("/healthz/ready", cts.Token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        return;
                    }
                }
                catch (HttpRequestException)
                {
                    // API not ready yet
                }

                await Task.Delay(HealthCheckPollIntervalMs, cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException($"API at {ApiBaseUrl} did not become healthy within {HealthCheckTimeoutSeconds}s");
        }
    }
}