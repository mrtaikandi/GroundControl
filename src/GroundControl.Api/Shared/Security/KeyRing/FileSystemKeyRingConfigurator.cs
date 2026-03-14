using Microsoft.AspNetCore.DataProtection;

namespace GroundControl.Api.Shared.Security.KeyRing;

/// <summary>
/// Persists Data Protection keys to the file system.
/// </summary>
internal sealed class FileSystemKeyRingConfigurator : IKeyRingConfigurator
{
    private const string DefaultKeyStorePath = "./keys";

    /// <inheritdoc />
    public void Configure(IDataProtectionBuilder builder, IConfiguration configuration)
    {
        var keyStorePath = configuration["DataProtection:KeyStorePath"] ?? DefaultKeyStorePath;
        var keyDirectory = new DirectoryInfo(keyStorePath);

        builder.PersistKeysToFileSystem(keyDirectory);

        if (OperatingSystem.IsWindows()
            && configuration.GetValue<bool>("DataProtection:UseDpapi"))
        {
            builder.ProtectKeysWithDpapi();
        }
    }
}