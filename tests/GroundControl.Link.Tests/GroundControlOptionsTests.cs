namespace GroundControl.Link.Tests;

public sealed class GroundControlOptionsTests
{
    [Fact]
    public void Scopes_DefaultsToEmptyDictionary()
    {
        // Arrange
        var options = new GroundControlOptions();

        // Act & Assert
        options.Scopes.ShouldNotBeNull();
        options.Scopes.ShouldBeEmpty();
    }

    [Fact]
    public void Scopes_AcceptsEntries()
    {
        // Arrange
        var options = new GroundControlOptions();

        // Act
        options.Scopes["environment"] = "production";
        options.Scopes["region"] = "us-east-1";

        // Assert
        options.Scopes.Count.ShouldBe(2);
        options.Scopes["environment"].ShouldBe("production");
        options.Scopes["region"].ShouldBe("us-east-1");
    }

    [Fact]
    public void Scopes_UsesCaseInsensitiveComparison()
    {
        // Arrange
        var options = new GroundControlOptions();

        // Act
        options.Scopes["Environment"] = "prod";

        // Assert
        options.Scopes.ContainsKey("Environment").ShouldBeTrue();
        options.Scopes.ContainsKey("environment").ShouldBeTrue();
    }
}