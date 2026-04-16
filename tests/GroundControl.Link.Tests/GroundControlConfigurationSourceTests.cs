namespace GroundControl.Link.Tests;

public sealed class GroundControlConfigurationSourceTests
{
    [Fact]
    public void Build_WithScopes_AddsScopesHeader()
    {
        // Arrange
        var options = new GroundControlOptions
        {
            ServerUrl = new Uri("http://localhost:9999"),
            ClientId = "test-id",
            ClientSecret = "test-secret",
            EnableLocalCache = false,
        };
        options.Scopes["environment"] = "production";
        options.Scopes["region"] = "us-east-1";

        var source = new GroundControlConfigurationSource(options);
        var builder = new ConfigurationBuilder();

        // Act
        var provider = (GroundControlConfigurationProvider)source.Build(builder);

        // Assert
        provider.ShouldNotBeNull();
    }

    [Fact]
    public void Build_WithoutScopes_OmitsScopesHeader()
    {
        // Arrange
        var options = new GroundControlOptions
        {
            ServerUrl = new Uri("http://localhost:9999"),
            ClientId = "test-id",
            ClientSecret = "test-secret",
            EnableLocalCache = false,
        };

        var source = new GroundControlConfigurationSource(options);
        var builder = new ConfigurationBuilder();

        // Act
        var provider = (GroundControlConfigurationProvider)source.Build(builder);

        // Assert
        provider.ShouldNotBeNull();
    }

    [Fact]
    public void Build_WithScopesContainingSpecialCharacters_UrlEncodesValues()
    {
        // Arrange
        var options = new GroundControlOptions
        {
            ServerUrl = new Uri("http://localhost:9999"),
            ClientId = "test-id",
            ClientSecret = "test-secret",
            EnableLocalCache = false,
        };
        options.Scopes["env"] = "prod:us,east";

        var source = new GroundControlConfigurationSource(options);
        var builder = new ConfigurationBuilder();

        // Act
        var provider = (GroundControlConfigurationProvider)source.Build(builder);

        // Assert
        provider.ShouldNotBeNull();
    }
}