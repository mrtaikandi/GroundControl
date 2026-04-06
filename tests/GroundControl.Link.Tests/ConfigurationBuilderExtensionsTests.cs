namespace GroundControl.Link.Tests;

public sealed class ConfigurationBuilderExtensionsTests
{
    [Fact]
    public void AddGroundControl_WithValidOptions_AddsSource()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act
        builder.AddGroundControl(options =>
        {
            options.ServerUrl = "https://test.example.com";
            options.ClientId = "test-client";
            options.ClientSecret = "test-secret";
        });

        // Assert
        builder.Sources.ShouldContain(s => s is GroundControlConfigurationSource);
    }

    [Fact]
    public void AddGroundControl_MissingServerUrl_ThrowsArgumentException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() =>
            builder.AddGroundControl(options =>
            {
                options.ClientId = "test-client";
                options.ClientSecret = "test-secret";
            }));

        ex.ParamName.ShouldBe("ServerUrl");
    }

    [Fact]
    public void AddGroundControl_MissingClientId_ThrowsArgumentException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() =>
            builder.AddGroundControl(options =>
            {
                options.ServerUrl = "https://test.example.com";
                options.ClientSecret = "test-secret";
            }));

        ex.ParamName.ShouldBe("ClientId");
    }

    [Fact]
    public void AddGroundControl_MissingClientSecret_ThrowsArgumentException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() =>
            builder.AddGroundControl(options =>
            {
                options.ServerUrl = "https://test.example.com";
                options.ClientId = "test-client";
            }));

        ex.ParamName.ShouldBe("ClientSecret");
    }

    [Fact]
    public void AddGroundControl_WhitespaceServerUrl_ThrowsArgumentException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.AddGroundControl(options =>
            {
                options.ServerUrl = "   ";
                options.ClientId = "test-client";
                options.ClientSecret = "test-secret";
            }));
    }

    [Fact]
    public void AddGroundControl_NullConfigure_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.AddGroundControl(null!));
    }

    [Fact]
    public void AddGroundControl_BuildsProviderWithoutException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();
        builder.AddGroundControl(options =>
        {
            options.ServerUrl = "https://test.example.com";
            options.ClientId = "test-client";
            options.ClientSecret = "test-secret";
        });

        // Act & Assert — Build() creates the provider
        var config = builder.Build();
        config.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(nameof(GroundControlOptions.StartupTimeout))]
    [InlineData(nameof(GroundControlOptions.PollingInterval))]
    [InlineData(nameof(GroundControlOptions.SseHeartbeatTimeout))]
    [InlineData(nameof(GroundControlOptions.SseReconnectDelay))]
    public void AddGroundControl_ZeroTimeSpan_ThrowsArgumentException(string propertyName)
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() =>
            builder.AddGroundControl(options =>
            {
                options.ServerUrl = "https://test.example.com";
                options.ClientId = "test-client";
                options.ClientSecret = "test-secret";
                typeof(GroundControlOptions).GetProperty(propertyName)!.SetValue(options, TimeSpan.Zero);
            }));

        ex.ParamName.ShouldBe(propertyName);
    }

    [Fact]
    public void AddGroundControl_SseMaxReconnectDelayLessThanReconnectDelay_ThrowsArgumentException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() =>
            builder.AddGroundControl(options =>
            {
                options.ServerUrl = "https://test.example.com";
                options.ClientId = "test-client";
                options.ClientSecret = "test-secret";
                options.SseReconnectDelay = TimeSpan.FromSeconds(10);
                options.SseMaxReconnectDelay = TimeSpan.FromSeconds(5);
            }));

        ex.ParamName.ShouldBe(nameof(GroundControlOptions.SseMaxReconnectDelay));
    }
}