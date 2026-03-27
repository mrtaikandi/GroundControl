using System.Net;
using System.Text.Json;
using GroundControl.Api.Shared.Notification;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Health;

[Collection("MongoDB")]
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
        var json = await ParseHealthResponseAsync(response);

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
        var json = await ParseHealthResponseAsync(response);

        // Assert
        var entries = json.GetProperty("entries");
        entries.TryGetProperty("mongodb", out _).ShouldBeTrue();
        entries.TryGetProperty("change_notifier", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task ReadyEndpoint_IncludesPerCheckStatusAndDuration()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready", TestCancellationToken);
        var json = await ParseHealthResponseAsync(response);

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
        var json = await ParseHealthResponseAsync(response);

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

    private static async Task<JsonElement> ParseHealthResponseAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement;
    }
}