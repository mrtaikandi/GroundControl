namespace GroundControl.Link.Tests.Internals;

public sealed class ConnectionHelpersTests
{
    [Fact]
    public void AddJitter_ReturnsValueBetween75And100Percent()
    {
        // Arrange
        var baseDelay = TimeSpan.FromSeconds(10);

        // Act & Assert — run multiple times to exercise the random range
        for (var i = 0; i < 100; i++)
        {
            var result = ConnectionHelpers.AddJitter(baseDelay);
            result.TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(7500);
            result.TotalMilliseconds.ShouldBeLessThanOrEqualTo(12500);
        }
    }

    [Fact]
    public void AddJitter_VerySmallDelay_ReturnsAtLeast100Ms()
    {
        // Arrange
        var baseDelay = TimeSpan.FromMilliseconds(10);

        // Act
        var result = ConnectionHelpers.AddJitter(baseDelay);

        // Assert
        result.TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(100);
    }

    [Fact]
    public void ParseConfigDataWithVersion_ValidJson_ReturnsEntriesAndVersion()
    {
        // Arrange
        var json = """{"data":{"Key1":"Value1","Key2":"Value2"},"snapshotVersion":42}""";

        // Act
        var (config, version) = ConnectionHelpers.ParseConfigDataWithVersion(json);

        // Assert
        config.ShouldContainKeyAndValue("Key1", "Value1");
        config.ShouldContainKeyAndValue("Key2", "Value2");
        version.ShouldBe("42");
    }

    [Fact]
    public void ParseConfigDataWithVersion_NoDataProperty_ReturnsEmpty()
    {
        // Arrange
        var json = """{"snapshotVersion":1}""";

        // Act
        var (config, version) = ConnectionHelpers.ParseConfigDataWithVersion(json);

        // Assert
        config.ShouldBeEmpty();
        version.ShouldBe("1");
    }

    [Fact]
    public void ParseConfigDataWithVersion_NoVersion_ReturnsNullVersion()
    {
        // Arrange
        var json = """{"data":{"K":"V"}}""";

        // Act
        var (config, version) = ConnectionHelpers.ParseConfigDataWithVersion(json);

        // Assert
        config.ShouldContainKeyAndValue("K", "V");
        version.ShouldBeNull();
    }

    [Fact]
    public void ParseConfigDataWithVersion_NestedData_Flattens()
    {
        // Arrange
        var json = """{"data":{"Logging":{"LogLevel":{"Default":"Warning"}}},"snapshotVersion":1}""";

        // Act
        var (config, _) = ConnectionHelpers.ParseConfigDataWithVersion(json);

        // Assert
        config.ShouldContainKeyAndValue("Logging:LogLevel:Default", "Warning");
    }
}