using Microsoft.Extensions.Options;

namespace GroundControl.Api.Core.DataProtection;

/// <summary>
/// Options describing how the Data Protection module authenticates to Azure. Used by the
/// Azure key ring configurator and the Azure Blob certificate provider.
/// </summary>
internal sealed class AzureCredentialOptions
{
    /// <summary>
    /// Gets or sets the credential type used to authenticate to Azure.
    /// </summary>
    public AzureCredentialType Mode { get; set; } = AzureCredentialType.Default;

    /// <summary>
    /// Gets or sets the Microsoft Entra tenant id. Required for
    /// <see cref="AzureCredentialType.ClientSecret"/> and
    /// <see cref="AzureCredentialType.WorkloadIdentity"/>.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the Microsoft Entra application (client) id. Required for
    /// <see cref="AzureCredentialType.ClientSecret"/> and
    /// <see cref="AzureCredentialType.WorkloadIdentity"/>; optional for
    /// <see cref="AzureCredentialType.ManagedIdentity"/> (selects a user-assigned identity).
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client secret. Required for <see cref="AzureCredentialType.ClientSecret"/>.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the path to a federated token file. Optional for
    /// <see cref="AzureCredentialType.WorkloadIdentity"/>; when omitted the credential reads the
    /// path from the <c>AZURE_FEDERATED_TOKEN_FILE</c> environment variable.
    /// </summary>
    public string? TokenFilePath { get; set; }

    /// <summary>
    /// Gets or sets the Microsoft Entra authority host URI. Use for sovereign clouds (for
    /// example <c>https://login.microsoftonline.us/</c>). When unset, the SDK default
    /// (<c>https://login.microsoftonline.com/</c>) is used.
    /// </summary>
    public Uri? AuthorityHost { get; set; }

    /// <summary>
    /// Validates <see cref="AzureCredentialOptions"/>. Source-generated validators only inspect
    /// data annotations; this validator enforces the cross-field rules each
    /// <see cref="AzureCredentialType"/> requires.
    /// </summary>
    internal sealed class Validator : IValidateOptions<AzureCredentialOptions>
    {
        /// <inheritdoc />
        public ValidateOptionsResult Validate(string? name, AzureCredentialOptions options)
        {
            var failures = new List<string>();
            var prefix = name is null ? nameof(AzureCredentialOptions) : $"{name}";

            switch (options.Mode)
            {
                case AzureCredentialType.ClientSecret:
                    RequireNonEmpty(options.TenantId, nameof(TenantId), prefix, options.Mode, failures);
                    RequireNonEmpty(options.ClientId, nameof(ClientId), prefix, options.Mode, failures);
                    RequireNonEmpty(options.ClientSecret, nameof(ClientSecret), prefix, options.Mode, failures);
                    break;

                case AzureCredentialType.WorkloadIdentity:
                    RequireNonEmpty(options.TenantId, nameof(TenantId), prefix, options.Mode, failures);
                    RequireNonEmpty(options.ClientId, nameof(ClientId), prefix, options.Mode, failures);
                    break;
            }

            return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
        }

        private static void RequireNonEmpty(string? value, string member, string prefix, AzureCredentialType mode, List<string> failures)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                failures.Add($"{prefix}:{member} is required when {nameof(Mode)} is '{mode}'.");
            }
        }
    }
}