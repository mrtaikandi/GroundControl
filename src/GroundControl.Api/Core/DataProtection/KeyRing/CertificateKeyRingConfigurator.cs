using Microsoft.AspNetCore.DataProtection;

namespace GroundControl.Api.Core.DataProtection.KeyRing;

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