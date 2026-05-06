namespace GroundControl.Api.Core.DataProtection;

/// <summary>
/// Defines how the Data Protection module authenticates to Azure when persisting key rings to
/// Azure Blob Storage / Key Vault, or downloading X.509 certificates from Azure Blob Storage.
/// </summary>
internal enum AzureCredentialType
{
    /// <summary>
    /// Uses <see cref="Azure.Identity.DefaultAzureCredential"/>, which probes a chain of credential
    /// sources (environment variables, workload identity, managed identity, Azure CLI, etc.) until
    /// one succeeds. Recommended for AKS / App Service deployments using Managed Identity.
    /// </summary>
    Default,

    /// <summary>
    /// Uses <see cref="Azure.Identity.ManagedIdentityCredential"/>. When
    /// <see cref="AzureCredentialOptions.ClientId"/> is set, authenticates as that user-assigned
    /// managed identity; otherwise authenticates as the system-assigned managed identity.
    /// </summary>
    ManagedIdentity,

    /// <summary>
    /// Uses <see cref="Azure.Identity.WorkloadIdentityCredential"/> for AKS workload identity.
    /// </summary>
    WorkloadIdentity,

    /// <summary>
    /// Uses <see cref="Azure.Identity.ClientSecretCredential"/>. Requires
    /// <see cref="AzureCredentialOptions.TenantId"/>, <see cref="AzureCredentialOptions.ClientId"/>,
    /// and <see cref="AzureCredentialOptions.ClientSecret"/>.
    /// </summary>
    ClientSecret,

    /// <summary>
    /// Uses <see cref="Azure.Identity.AzureCliCredential"/> for local development against an
    /// authenticated Azure CLI session.
    /// </summary>
    AzureCli,

    /// <summary>
    /// Uses <see cref="Azure.Identity.EnvironmentCredential"/>, reading credentials from the
    /// well-known <c>AZURE_*</c> environment variables.
    /// </summary>
    Environment
}