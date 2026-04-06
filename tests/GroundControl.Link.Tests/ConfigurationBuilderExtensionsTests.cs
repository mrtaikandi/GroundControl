using Microsoft.Extensions.Options;

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
    public void AddGroundControl_MissingServerUrl_ThrowsOptionsValidationException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        var ex = Should.Throw<OptionsValidationException>(() =>
            builder.AddGroundControl(options =>
            {
                options.ClientId = "test-client";
                options.ClientSecret = "test-secret";
            }));

        ex.Failures.ShouldContain(f => f.Contains("ServerUrl"));
    }

    [Fact]
    public void AddGroundControl_MissingClientId_ThrowsOptionsValidationException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        var ex = Should.Throw<OptionsValidationException>(() =>
            builder.AddGroundControl(options =>
            {
                options.ServerUrl = "https://test.example.com";
                options.ClientSecret = "test-secret";
            }));

        ex.Failures.ShouldContain(f => f.Contains("ClientId"));
    }

    [Fact]
    public void AddGroundControl_MissingClientSecret_ThrowsOptionsValidationException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        var ex = Should.Throw<OptionsValidationException>(() =>
            builder.AddGroundControl(options =>
            {
                options.ServerUrl = "https://test.example.com";
                options.ClientId = "test-client";
            }));

        ex.Failures.ShouldContain(f => f.Contains("ClientSecret"));
    }

    [Fact]
    public void AddGroundControl_WhitespaceServerUrl_ThrowsOptionsValidationException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        Should.Throw<OptionsValidationException>(() =>
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
    public void AddGroundControl_ZeroTimeSpan_ThrowsOptionsValidationException(string propertyName)
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        var ex = Should.Throw<OptionsValidationException>(() =>
            builder.AddGroundControl(options =>
            {
                options.ServerUrl = "https://test.example.com";
                options.ClientId = "test-client";
                options.ClientSecret = "test-secret";
                typeof(GroundControlOptions).GetProperty(propertyName)!.SetValue(options, TimeSpan.Zero);
            }));

        ex.Failures.ShouldContain(f => f.Contains(propertyName));
    }

    [Fact]
    public void AddGroundControl_SseMaxReconnectDelayLessThanReconnectDelay_ThrowsOptionsValidationException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        var ex = Should.Throw<OptionsValidationException>(() =>
            builder.AddGroundControl(options =>
            {
                options.ServerUrl = "https://test.example.com";
                options.ClientId = "test-client";
                options.ClientSecret = "test-secret";
                options.SseReconnectDelay = TimeSpan.FromSeconds(10);
                options.SseMaxReconnectDelay = TimeSpan.FromSeconds(5);
            }));

        ex.Failures.ShouldContain(f => f.Contains(nameof(GroundControlOptions.SseMaxReconnectDelay)));
    }
}