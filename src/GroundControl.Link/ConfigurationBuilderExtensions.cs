using Microsoft.Extensions.Options;

namespace GroundControl.Link;

/// <summary>
/// Extension methods for <see cref="IConfigurationBuilder"/> to register GroundControl as a configuration source.
/// </summary>
public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Adds GroundControl as a configuration source.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="configure">A delegate to configure <see cref="GroundControlOptions"/>.</param>
    /// <returns>The configuration builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configure"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when required options (ServerUrl, ClientId, ClientSecret) are missing.</exception>
    public static IConfigurationBuilder AddGroundControl(
        this IConfigurationBuilder builder,
        Action<GroundControlOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new GroundControlOptions();
        configure(options);

        var result = new GroundControlOptions.Validator().Validate(null, options);
        return result.Failed
            ? throw new OptionsValidationException(nameof(GroundControlOptions), typeof(GroundControlOptions), result.Failures)
            : builder.Add(new GroundControlConfigurationSource(options));
    }
}