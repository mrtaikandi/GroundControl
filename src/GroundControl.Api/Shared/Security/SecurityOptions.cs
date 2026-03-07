using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Shared.Security;

/// <summary>
/// Configuration options for security and authentication.
/// </summary>
internal sealed partial class SecurityOptions
{
    /// <summary>
    /// Gets or sets the authentication mode.
    /// </summary>
    [Required]
    public AuthenticationMode AuthenticationMode { get; set; } = AuthenticationMode.None;

    [OptionsValidator]
    internal sealed partial class Validator : IValidateOptions<SecurityOptions>;
}