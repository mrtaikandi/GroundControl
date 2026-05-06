using Azure.Core;
using Azure.Identity;

namespace GroundControl.Api.Core.DataProtection;

/// <summary>
/// Builds an Azure SDK <see cref="TokenCredential"/> from <see cref="AzureCredentialOptions"/>.
/// </summary>
internal static class AzureCredentialFactory
{
    /// <summary>
    /// Creates a <see cref="TokenCredential"/> matching the configured
    /// <see cref="AzureCredentialOptions.Mode"/>. The caller must validate
    /// <paramref name="options"/> before invoking; required fields are not re-checked here.
    /// </summary>
    public static TokenCredential Create(AzureCredentialOptions options) => options.Mode switch
    {
        AzureCredentialType.Default => new DefaultAzureCredential(BuildDefaultOptions(options)),
        AzureCredentialType.ManagedIdentity => CreateManagedIdentity(options),
        AzureCredentialType.WorkloadIdentity => CreateWorkloadIdentity(options),
        AzureCredentialType.ClientSecret => new ClientSecretCredential(options.TenantId!, options.ClientId!, options.ClientSecret!, BuildTokenCredentialOptions<ClientSecretCredentialOptions>(options)),
        AzureCredentialType.AzureCli => new AzureCliCredential(BuildTokenCredentialOptions<AzureCliCredentialOptions>(options)),
        AzureCredentialType.Environment => new EnvironmentCredential(BuildTokenCredentialOptions<EnvironmentCredentialOptions>(options)),
        _ => throw new InvalidOperationException(
            $"Unknown {nameof(AzureCredentialOptions)}:{nameof(AzureCredentialOptions.Mode)} '{options.Mode}'. Supported values: {string.Join(", ", Enum.GetNames<AzureCredentialType>())}.")
    };

    private static ManagedIdentityCredential CreateManagedIdentity(AzureCredentialOptions options)
    {
        var managedIdentityId = string.IsNullOrWhiteSpace(options.ClientId)
            ? ManagedIdentityId.SystemAssigned
            : ManagedIdentityId.FromUserAssignedClientId(options.ClientId);

        var credentialOptions = new ManagedIdentityCredentialOptions(managedIdentityId);
        if (options.AuthorityHost is not null)
        {
            credentialOptions.AuthorityHost = options.AuthorityHost;
        }

        return new ManagedIdentityCredential(credentialOptions);
    }

    private static WorkloadIdentityCredential CreateWorkloadIdentity(AzureCredentialOptions options)
    {
        var credentialOptions = new WorkloadIdentityCredentialOptions
        {
            TenantId = options.TenantId,
            ClientId = options.ClientId,
            TokenFilePath = options.TokenFilePath
        };

        if (options.AuthorityHost is not null)
        {
            credentialOptions.AuthorityHost = options.AuthorityHost;
        }

        return new WorkloadIdentityCredential(credentialOptions);
    }

    private static DefaultAzureCredentialOptions BuildDefaultOptions(AzureCredentialOptions options)
    {
        var defaultOptions = new DefaultAzureCredentialOptions
        {
            TenantId = options.TenantId,
            ManagedIdentityClientId = options.ClientId
        };

        if (options.AuthorityHost is not null)
        {
            defaultOptions.AuthorityHost = options.AuthorityHost;
        }

        return defaultOptions;
    }

    private static T BuildTokenCredentialOptions<T>(AzureCredentialOptions options)
        where T : TokenCredentialOptions, new()
    {
        var credentialOptions = new T();
        if (options.AuthorityHost is not null)
        {
            credentialOptions.AuthorityHost = options.AuthorityHost;
        }

        return credentialOptions;
    }
}