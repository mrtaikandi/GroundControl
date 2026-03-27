using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;

namespace GroundControl.Api.Shared.Security.KeyRing;

/// <summary>
/// Persists Data Protection keys to Azure Blob Storage and protects them with Azure Key Vault.
/// </summary>
internal sealed class AzureKeyRingConfigurator : IKeyRingConfigurator
{
    private static readonly DefaultAzureCredential Credential = new();

    /// <inheritdoc />
    public void Configure(IDataProtectionBuilder builder, IConfiguration configuration)
    {
        var blobUri = configuration["DataProtection:Azure:BlobUri"]
            ?? throw new InvalidOperationException("DataProtection:Azure:BlobUri is required for Azure mode.");

        var keyVaultKeyId = configuration["DataProtection:Azure:KeyVaultKeyId"]
            ?? throw new InvalidOperationException("DataProtection:Azure:KeyVaultKeyId is required for Azure mode.");

        builder
            .PersistKeysToAzureBlobStorage(new Uri(blobUri), Credential)
            .ProtectKeysWithAzureKeyVault(new Uri(keyVaultKeyId), Credential);
    }
}