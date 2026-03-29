using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Shared.Security.DataProtection;

/// <summary>
/// Azure-specific options for Data Protection key ring storage.
/// </summary>
internal sealed partial class AzureOptions
{
    /// <summary>
    /// Gets or sets the Azure Blob Storage URI for key persistence.
    /// </summary>
    [Required]
    public Uri? BlobUri { get; set; }

    /// <summary>
    /// Gets or sets the Azure Key Vault key identifier for key encryption.
    /// </summary>
    [Required]
    public Uri? KeyVaultKeyId { get; set; }

    [OptionsValidator]
    public partial class Validator : IValidateOptions<AzureOptions>;
}