using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Core.Authentication.External;

/// <summary>
/// Configuration options for CSRF protection.
/// </summary>
internal sealed partial class CsrfOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether CSRF protection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the name of the CSRF cookie.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string CookieName { get; set; } = "XSRF-TOKEN";

    /// <summary>
    /// Gets or sets the name of the CSRF header.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string HeaderName { get; set; } = "X-XSRF-TOKEN";

    [OptionsValidator]
    public sealed partial class Validator : IValidateOptions<CsrfOptions>;
}