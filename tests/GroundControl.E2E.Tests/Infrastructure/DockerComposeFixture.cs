using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace GroundControl.E2E.Tests.Infrastructure;

/// <summary>
/// Assembly-level fixture that manages the Docker Compose lifecycle for E2E tests.
/// Starts the stack in InitializeAsync, tears it down in DisposeAsync.
/// When E2E_RUNNING_IN_DOCKER is set, skips compose management and reads E2E_API_URL.
/// </summary>
public sealed class DockerComposeFixture : IAsyncLifetime
{
    private const int HealthCheckTimeoutSeconds = 120;
    private const int HealthCheckPollIntervalMs = 2000;

    private readonly string _composeFilePath;
    private readonly bool _isRunningInDocker;

    public DockerComposeFixture()
    {
        _isRunningInDocker = Environment.GetEnvironmentVariable("E2E_RUNNING_IN_DOCKER") == "true";

        // Resolve compose file from the source tree via RepositoryRoot metadata,
        // so Docker build context paths in the compose file resolve correctly.
        var repoRoot = typeof(DockerComposeFixture).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "RepositoryRoot")?.Value
            ?? throw new InvalidOperationException("RepositoryRoot assembly metadata not found.");

        _composeFilePath = Path.Combine(repoRoot, "tests", "GroundControl.E2E.Tests", "docker-compose.yml");
    }

    /// <summary>
    /// Gets the base URL of the API server (e.g., http://localhost:32789).
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "String URL is the design contract for consumer convenience")]
    public string ApiBaseUrl { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        if (_isRunningInDocker)
        {
            ApiBaseUrl = Environment.GetEnvironmentVariable("E2E_API_URL") ?? "http://api:8080";
            return;
        }

        await CliRunner.BuildAsync().ConfigureAwait(false);
        await RunComposeAsync("up", "-d", "--build", "--wait").ConfigureAwait(false);
        ApiBaseUrl = await DiscoverApiPortAsync().ConfigureAwait(false);
        await WaitForHealthAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_isRunningInDocker)
        {
            return;
        }

        await RunComposeAsync("down", "-v").ConfigureAwait(false);
    }

    private async Task<string> DiscoverApiPortAsync()
    {
        var result = await RunComposeWithOutputAsync("port", "api", "8080").ConfigureAwait(false);
        var output = result.Trim();

        // Output is like "0.0.0.0:32789" -- extract the port
        var colonIndex = output.LastIndexOf(':');
        if (colonIndex < 0)
        {
            throw new InvalidOperationException($"Unexpected 'docker compose port' output: {output}");
        }

        var port = output[(colonIndex + 1)..];

        // Use 127.0.0.1 instead of localhost to avoid IPv6 resolution issues.
        // Docker maps ports to 0.0.0.0 (IPv4 only), but localhost may resolve
        // to ::1 (IPv6) first on Windows, causing .NET's HttpClient to hang
        // until the connection attempt times out.
        return $"http://127.0.0.1:{port}";
    }

    private async Task WaitForHealthAsync()
    {
        // ReSharper disable once ShortLivedHttpClient
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(ApiBaseUrl);
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

    private async Task RunComposeAsync(params string[] args)
    {
        var options = CreateComposeOptions(args);
        var result = await ProcessRunner.RunAsync(options).ConfigureAwait(false);
        result.ThrowIfFailed($"docker compose {string.Join(' ', args)} failed");
    }

    private async Task<string> RunComposeWithOutputAsync(params string[] args)
    {
        var options = CreateComposeOptions(args);
        var result = await ProcessRunner.RunAsync(options).ConfigureAwait(false);
        result.ThrowIfFailed($"docker compose {string.Join(' ', args)} failed");
        return result.Stdout;
    }

    private ProcessRunOptions CreateComposeOptions(string[] args) =>
        new()
        {
            FileName = "docker",
            Arguments = $"compose -f \"{_composeFilePath}\" {string.Join(' ', args)}",
            WorkingDirectory = Path.GetDirectoryName(_composeFilePath)!
        };
}