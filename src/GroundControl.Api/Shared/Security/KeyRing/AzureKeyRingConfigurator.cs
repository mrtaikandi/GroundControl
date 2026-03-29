using Azure.Identity;
using GroundControl.Api.Shared.Extensions.Options;
using GroundControl.Api.Shared.Security.DataProtection;
using Microsoft.AspNetCore.DataProtection;
using DataProtectionOptions = GroundControl.Api.Shared.Security.DataProtection.DataProtectionOptions;

namespace GroundControl.Api.Shared.Security.KeyRing;

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