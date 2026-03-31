using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace GroundControl.Host.Cli.Logging;

/// <summary>
/// Provides extension methods for configuring logging providers.
/// </summary>
public static class LoggingBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="SpectreConsoleLoggerProvider"/> as an <see cref="ILoggerProvider"/>.
    /// </summary>
    /// <param name="builder">The logging builder used to configure logging providers.</param>
    /// <returns>The same <see cref="ILoggingBuilder"/> instance for chaining.</returns>
    public static ILoggingBuilder AddSpectreConsole(this ILoggingBuilder builder)
    {
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider, SpectreConsoleLoggerProvider>());

        return builder;
    }
}