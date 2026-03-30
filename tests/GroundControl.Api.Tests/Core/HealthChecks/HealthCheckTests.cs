using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using GroundControl.Api.Core.ChangeNotification;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.HealthChecks;

public sealed class HealthCheckTests : ApiHandlerTestBase
{
    public HealthCheckTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task LivenessEndpoint_AlwaysReturns200()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/liveness", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadyEndpoint_WhenAllHealthy_Returns200()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadyEndpoint_WhenAllHealthy_ReturnsHealthyStatus()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready", TestCancellationToken);
        var json = await ParseHealthResponseAsync(response, TestCancellationToken);

        // Assert
        json.GetProperty("status").GetString().ShouldBe("Healthy");
    }

    [Fact]
    public async Task ReadyEndpoint_IncludesMongoDbAndChangeNotifierEntries()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready", TestCancellationToken);
        var json = await ParseHealthResponseAsync(response, TestCancellationToken);

        // Assert
        var entries = json.GetProperty("entries");
        entries.TryGetProperty("mongodb", out _).ShouldBeTrue();
        entries.TryGetProperty("change-notifier", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task ReadyEndpoint_IncludesPerCheckStatusAndDuration()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready", TestCancellationToken);
        var json = await ParseHealthResponseAsync(response, TestCancellationToken);

        // Assert
        var entries = json.GetProperty("entries");

        foreach (var entry in entries.EnumerateObject())
        {
            entry.Value.TryGetProperty("status", out _).ShouldBeTrue($"Entry '{entry.Name}' should have a status");
            entry.Value.TryGetProperty("duration", out _).ShouldBeTrue($"Entry '{entry.Name}' should have a duration");
        }
    }

    [Fact]
    public async Task ReadyEndpoint_DoesNotContainConnectionStrings()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready", TestCancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestCancellationToken);

        // Assert
        body.ShouldNotContain("mongodb://");
        body.ShouldNotContain("ConnectionString");
    }

    [Fact]
    public async Task ReadyEndpoint_WhenChangeNotifierDisposed_Returns503()
    {
        // Arrange
        await using var factory = CreateFactory();
        var notifier = (InProcessChangeNotifier)factory.Services.GetRequiredService<IChangeNotifier>();
        await notifier.DisposeAsync();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task ReadyEndpoint_WhenChangeNotifierDisposed_ReturnsUnhealthyStatus()
    {
        // Arrange
        await using var factory = CreateFactory();
        var notifier = (InProcessChangeNotifier)factory.Services.GetRequiredService<IChangeNotifier>();
        await notifier.DisposeAsync();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready", TestCancellationToken);
        var json = await ParseHealthResponseAsync(response, TestCancellationToken);

        // Assert
        json.GetProperty("status").GetString().ShouldBe("Unhealthy");
    }

    [Fact]
    public async Task LivenessEndpoint_WhenChangeNotifierDisposed_StillReturns200()
    {
        // Arrange
        await using var factory = CreateFactory();
        var notifier = (InProcessChangeNotifier)factory.Services.GetRequiredService<IChangeNotifier>();
        await notifier.DisposeAsync();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/liveness", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadyEndpoint_WhenMongoDbUnreachable_Returns503()
    {
        // Arrange — use unreachable MongoDB with short timeouts; remove hosted services
        // that would fail on startup before the health check can run
        await using var factory = CreateFactoryWithUnreachableMongo();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task LivenessEndpoint_WhenMongoDbUnreachable_StillReturns200()
    {
        // Arrange
        await using var factory = CreateFactoryWithUnreachableMongo();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/liveness", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "WithWebHostBuilder wraps and disposes the inner factory")]
    private static WebApplicationFactory<Program> CreateFactoryWithUnreachableMongo()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Storage"] = "mongodb://localhost:1/?connectTimeoutMS=1000&serverSelectionTimeoutMS=1000",
                        ["Persistence:MongoDb:DatabaseName"] = "unreachable_db",
                        ["Authentication:AuthenticationMode"] = "None"
                    });
                });
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IHostedService>();
                });
            });
    }

    private static async Task<JsonElement> ParseHealthResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(body).RootElement;
    }
}