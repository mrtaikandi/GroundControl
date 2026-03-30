using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;

namespace GroundControl.Api.Core.DataProtection.KeyRing;

/// <summary>
/// Persists Data Protection keys to Azure Blob Storage and protects them with Azure Key Vault.
/// </summary>
internal sealed class AzureKeyRingConfigurator : IKeyRingConfigurator
{
    private static readonly DefaultAzureCredential Credential = new();

    /// <inheritdoc />
    public void Configure(IDataProtectionBuilder builder, DataProtectionOptions options)
    {
        AzureOptions.Validator.ThrowIfInvalid(options.Azure);

        builder
            .PersistKeysToAzureBlobStorage(options.Azure.BlobUri, Credential)
            .ProtectKeysWithAzureKeyVault(options.Azure.KeyVaultKeyId, Credential);
    }
}