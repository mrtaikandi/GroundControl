using Microsoft.AspNetCore.DataProtection;

namespace GroundControl.Api.Shared.Security.KeyRing;

/// <summary>
/// Configures the Data Protection key ring storage backend.
/// </summary>
public interface IKeyRingConfigurator
{
    /// <summary>
    /// Applies key ring storage configuration to the specified Data Protection builder.
    /// </summary>
    /// <param name="builder">The Data Protection builder to configure.</param>
    /// <param name="configuration">The application configuration.</param>
    void Configure(IDataProtectionBuilder builder, IConfiguration configuration);
}