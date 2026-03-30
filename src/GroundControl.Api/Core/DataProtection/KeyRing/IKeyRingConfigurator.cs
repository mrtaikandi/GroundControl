using Microsoft.AspNetCore.DataProtection;

namespace GroundControl.Api.Core.DataProtection.KeyRing;

/// <summary>
/// Configures the Data Protection key ring storage backend.
/// </summary>
internal interface IKeyRingConfigurator
{
    /// <summary>
    /// Applies key ring storage configuration to the specified Data Protection builder.
    /// </summary>
    /// <param name="builder">The Data Protection builder to configure.</param>
    /// <param name="options">The Data Protection options.</param>
    void Configure(IDataProtectionBuilder builder, DataProtectionOptions options);
}