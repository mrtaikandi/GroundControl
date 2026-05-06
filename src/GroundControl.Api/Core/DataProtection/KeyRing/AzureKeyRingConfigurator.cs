using Azure.Core;
using Microsoft.AspNetCore.DataProtection;

namespace GroundControl.Api.Core.DataProtection.KeyRing;

/// <summary>
/// Persists Data Protection keys to Azure Blob Storage and protects them with Azure Key Vault.
/// </summary>
internal sealed class AzureKeyRingConfigurator(TokenCredential credential) : IKeyRingConfigurator
{
    /// <inheritdoc />
    public void Configure(IDataProtectionBuilder builder, DataProtectionOptions options)
    {
        AzureOptions.Validator.ThrowIfInvalid(options.Azure);

        builder
            .PersistKeysToAzureBlobStorage(options.Azure.BlobUri, credential)
            .ProtectKeysWithAzureKeyVault(options.Azure.KeyVaultKeyId, credential);
    }
}