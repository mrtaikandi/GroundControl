using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Core.DataProtection.Certificate;

/// <summary>
/// Options for the <see cref="AzureBlobCertificateProvider"/>.
/// </summary>
internal sealed partial class AzureBlobCertificateOptions
{
    /// <summary>
    /// Gets or sets the Azure Blob Storage URI of the current X.509 certificate (PFX/PKCS#12).
    /// </summary>
    [Required]
    public Uri? BlobUri { get; set; }

    /// <summary>
    /// Gets or sets the password used to load the current and previous certificates.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets blob URIs of previous certificates retained for decrypting key XML
    /// produced under earlier certificates during rotation.
    /// </summary>
    public Uri[] PreviousBlobUris { get; set; } = [];

    [OptionsValidator]
    public sealed partial class Validator : IValidateOptions<AzureBlobCertificateOptions>;
}