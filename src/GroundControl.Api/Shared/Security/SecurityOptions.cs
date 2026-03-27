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

    /// <summary>
    /// Gets or sets the built-in authentication options.
    /// </summary>
    public BuiltInSecurityOptions BuiltIn { get; set; } = new();

    /// <summary>
    /// Gets or sets the CSRF protection options.
    /// </summary>
    public CsrfOptions Csrf { get; set; } = new();
  
    /// <summary>
    /// Gets or sets the external OIDC authentication options.
    /// </summary>
    public ExternalSecurityOptions External { get; set; } = new();

    /// <summary>
    /// Gets or sets the admin seed options.
    /// </summary>
    public SeedOptions Seed { get; set; } = new();

    [OptionsValidator]
    internal sealed partial class Validator : IValidateOptions<SecurityOptions>;
}