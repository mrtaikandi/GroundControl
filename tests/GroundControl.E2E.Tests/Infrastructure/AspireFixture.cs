using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace GroundControl.E2E.Tests.Infrastructure;

/// <summary>
/// Assembly-level fixture that manages the Aspire distributed application lifecycle for E2E tests.
/// Uses <see cref="DistributedApplicationTestingBuilder"/> for proper Aspire test hosting.
/// </summary>
public sealed class AspireFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    /// <summary>
    /// Gets the running Aspire distributed application.
    /// Use <c>App.CreateHttpClient("api")</c> to obtain HTTP clients with built-in service discovery.
    /// </summary>
    public DistributedApplication App => _app ?? throw new InvalidOperationException("App not started");

    /// <summary>
    /// Gets the base URL of the API server (e.g., http://localhost:12345).
    /// Used by <see cref="CliRunner"/> which needs a string URL for the environment variable.
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "String URL is the design contract for consumer convenience")]
    public string ApiBaseUrl { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await CliRunner.InitializeAsync().ConfigureAwait(false);

        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.GroundControl_AppHost>().ConfigureAwait(false);

        // Remove data volume and persistent lifetime from MongoDB so each test run starts clean
        var mongo = appHost.Resources.Single(r => r.Name == "mongo");
        foreach (var annotation in mongo.Annotations.OfType<ContainerMountAnnotation>().ToList())
        {
            mongo.Annotations.Remove(annotation);
        }

        foreach (var annotation in mongo.Annotations.OfType<ContainerLifetimeAnnotation>().ToList())
        {
            mongo.Annotations.Remove(annotation);
        }

        var api = appHost.Resources.Single(r => r.Name == "api");
        api.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            context.EnvironmentVariables["Snapshots__RetentionCount"] = "3"));

        appHost.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddFakeLogging(c => c.OutputSink = message => TestContext.Current.TestOutputHelper?.WriteLine(message));

            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Debug);
            logging.AddFilter<FakeLoggerProvider>(l => l >= LogLevel.Debug);
        });

        _app = await appHost.BuildAsync().ConfigureAwait(false);
        await _app.StartAsync().ConfigureAwait(false);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("api", cts.Token).ConfigureAwait(false);

        // Extract URL for CliRunner (needs string URL for environment variable)
        using var httpClient = _app.CreateHttpClient("api");
        ApiBaseUrl = httpClient.BaseAddress?.GetLeftPart(UriPartial.Authority) ?? throw new InvalidOperationException("API base URL is null");
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync().ConfigureAwait(false);
        }
    }
}