using Microsoft.AspNetCore.DataProtection;
using DataProtectionOptions = GroundControl.Api.Shared.Security.DataProtection.DataProtectionOptions;

namespace GroundControl.Api.Shared.Security.KeyRing;

/// <summary>
/// Persists Data Protection keys to the file system.
/// </summary>
internal sealed class FileSystemKeyRingConfigurator : IKeyRingConfigurator
{
    /// <inheritdoc />
    public void Configure(IDataProtectionBuilder builder, DataProtectionOptions options)
    {
        var keyDirectory = new DirectoryInfo(options.KeyStorePath);

        builder.PersistKeysToFileSystem(keyDirectory);

        if (OperatingSystem.IsWindows() && options.UseDpapi)
        {
            builder.ProtectKeysWithDpapi();
        }
    }
}