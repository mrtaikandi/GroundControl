using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Core.DataProtection.Certificate;

/// <summary>
/// Options for the <see cref="FileSystemCertificateProvider"/>.
/// </summary>
internal sealed partial class FileSystemCertificateOptions
{
    /// <summary>
    /// Gets or sets the file system path to the current X.509 certificate (PFX/PKCS#12).
    /// </summary>
    [Required]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password used to load the current and previous certificates.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets file system paths to previous certificates retained for decrypting key XML
    /// produced under earlier certificates during rotation.
    /// </summary>
    public string[] PreviousPaths { get; set; } = [];

    [OptionsValidator]
    public sealed partial class Validator : IValidateOptions<FileSystemCertificateOptions>;
}