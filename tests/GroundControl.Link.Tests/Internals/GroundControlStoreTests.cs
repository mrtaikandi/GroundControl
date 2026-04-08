using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GroundControl.Link.Tests.Internals;

public sealed class GroundControlStoreTests
{
    private static GroundControlOptions CreateOptions() => new()
    {
        ServerUrl = new Uri("http://localhost"),
        ClientId = "test",
        ClientSecret = "secret"
    };

    [Fact]
    public void Constructor_SetsOptions()
    {
        // Arrange
        var options = CreateOptions();

        // Act
        var store = new GroundControlStore(options);

        // Assert
        store.Options.ShouldBeSameAs(options);
    }

    [Fact]
    public void GetSnapshot_Initially_ReturnsEmptyData()
    {
        // Arrange
        var store = new GroundControlStore(CreateOptions());

        // Act
        var snapshot = store.GetSnapshot();

        // Assert
        snapshot.Data.ShouldBeEmpty();
        snapshot.ETag.ShouldBeNull();
        snapshot.LastEventId.ShouldBeNull();
    }

    [Fact]
    public void HealthStatus_Initially_Unhealthy()
    {
        // Arrange & Act
        var store = new GroundControlStore(CreateOptions());

        // Assert
        store.HealthStatus.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public void Update_SwapsSnapshotAndMarksHealthy()
    {
        // Arrange
        var store = new GroundControlStore(CreateOptions());
        var data = new Dictionary<string, string> { ["Key1"] = "Value1" };

        // Act
        store.Update(data, "etag-1", "event-1");

        // Assert
        var snapshot = store.GetSnapshot();
        snapshot.Data.ShouldContainKeyAndValue("Key1", "Value1");
        snapshot.ETag.ShouldBe("etag-1");
        snapshot.LastEventId.ShouldBe("event-1");
        store.HealthStatus.ShouldBe(HealthStatus.Healthy);
        store.LastSuccessfulUpdate.ShouldNotBeNull();
    }

    [Fact]
    public void Update_RaisesOnDataChanged()
    {
        // Arrange
        var store = new GroundControlStore(CreateOptions());
        var raised = false;
        store.OnDataChanged += () => raised = true;

        // Act
        store.Update(new Dictionary<string, string> { ["K"] = "V" }, null, null);

        // Assert
        raised.ShouldBeTrue();
    }

    [Fact]
    public void Update_NoSubscribers_DoesNotThrow()
    {
        // Arrange
        var store = new GroundControlStore(CreateOptions());

        // Act & Assert
        Should.NotThrow(() => store.Update(new Dictionary<string, string>(), null, null));
    }

    [Fact]
    public void SetHealth_ChangesHealthStatus()
    {
        // Arrange
        var store = new GroundControlStore(CreateOptions());

        // Act
        store.SetHealth(HealthStatus.Degraded);

        // Assert
        store.HealthStatus.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public void SetHealth_WithError_StoresReasonAndException()
    {
        // Arrange
        var store = new GroundControlStore(CreateOptions());
        var error = new HttpRequestException("Connection refused");

        // Act
        store.SetHealth(HealthStatus.Unhealthy, "Server unreachable", error);

        // Assert
        store.LastErrorReason.ShouldBe("Server unreachable");
        store.LastError.ShouldBeSameAs(error);
    }

    [Fact]
    public void Update_ClearsLastErrorAndReason()
    {
        // Arrange
        var store = new GroundControlStore(CreateOptions());
        store.SetHealth(HealthStatus.Unhealthy, "fail", new HttpRequestException("fail"));

        // Act
        store.Update(new Dictionary<string, string> { ["K"] = "V" }, "\"1\"", null);

        // Assert
        store.LastError.ShouldBeNull();
        store.LastErrorReason.ShouldBeNull();
    }

    [Fact]
    public void Update_OverwritesPreviousSnapshot()
    {
        // Arrange
        var store = new GroundControlStore(CreateOptions());
        store.Update(new Dictionary<string, string> { ["Old"] = "1" }, "etag-1", null);

        // Act
        store.Update(new Dictionary<string, string> { ["New"] = "2" }, "etag-2", "event-2");

        // Assert
        var snapshot = store.GetSnapshot();
        snapshot.Data.ShouldNotContainKey("Old");
        snapshot.Data.ShouldContainKeyAndValue("New", "2");
        snapshot.ETag.ShouldBe("etag-2");
    }
}