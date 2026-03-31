using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GroundControl.Host.Cli;

/// <summary>
/// Defines a module that registers services into the dependency injection container.
/// </summary>
public interface IDependencyModule
{
    /// <summary>
    /// Configures services for the current module using host and configuration context.
    /// </summary>
    /// <param name="context">The module context containing the host environment and configuration.</param>
    /// <param name="services">The service collection to register dependencies into.</param>
    void ConfigureServices(DependencyModuleContext context, IServiceCollection services);
}

/// <summary>
/// Provides contextual information for dependency module service registration.
/// </summary>
/// <param name="Environment">The current host environment.</param>
/// <param name="Configuration">The application configuration.</param>
public readonly record struct DependencyModuleContext(IHostEnvironment Environment, IConfiguration Configuration);