using Microsoft.AspNetCore.DataProtection;
using DataProtectionOptions = GroundControl.Api.Shared.Security.DataProtection.DataProtectionOptions;

namespace GroundControl.Api.Shared.Security.KeyRing;

/// <summary>
/// Persists Data Protection keys to the file system.
/// Certificate-based key encryption is handled separately by
/// <see cref="Certificate.CertificateKeyEncryptionConfigurator"/>.
/// </summary>
internal sealed class CertificateKeyRingConfigurator : IKeyRingConfigurator
{
    /// <inheritdoc />
    public void Configure(IDataProtectionBuilder builder, DataProtectionOptions options)
    {
        var keyDirectory = new DirectoryInfo(options.KeyStorePath);
        builder.PersistKeysToFileSystem(keyDirectory);
    }
}