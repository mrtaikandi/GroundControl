using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Core.Authentication.BuiltIn;
using GroundControl.Api.Core.Authentication.External;
using GroundControl.Api.Core.Authentication.NoAuth;
using GroundControl.Api.Shared.Extensions.Options;
using GroundControl.Host.Api;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Core.Authentication;

/// <summary>
/// Configuration options for authentication and authorization.
/// </summary>
[ConfigurationKey(SectionName)]
internal sealed class AuthenticationOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "Authentication";

    /// <summary>
    /// Gets or sets the authentication mode.
    /// </summary>
    [Required]
    public AuthenticationMode Mode { get; set; } = AuthenticationMode.None;

    /// <summary>
    /// Gets or sets the built-in authentication options.
    /// </summary>
    public BuiltInAuthenticationOptions BuiltIn { get; set; } = new();

    /// <summary>
    /// Gets or sets the CSRF protection options.
    /// </summary>
    public CsrfOptions Csrf { get; set; } = new();

    /// <summary>
    /// Gets or sets the external OIDC authentication options.
    /// </summary>
    public ExternalAuthenticationOptions External { get; set; } = new();

    /// <summary>
    /// Gets or sets the admin seed options.
    /// </summary>
    public SeedOptions Seed { get; set; } = new();

    /// <summary>
    /// Validates <see cref="AuthenticationOptions"/> including cross-property constraints based on the selected authentication mode.
    /// </summary>
    internal sealed class Validator : IValidateOptions<AuthenticationOptions>
    {
        /// <inheritdoc />
        public ValidateOptionsResult Validate(string? name, AuthenticationOptions options)
        {
            var result = options.Mode switch
            {
                AuthenticationMode.BuiltIn => new BuiltInAuthenticationOptions.Validator().Validate(nameof(options.BuiltIn), options.BuiltIn),
                AuthenticationMode.External => new ExternalAuthenticationOptions.Validator().Validate(nameof(options.External), options.External),
                AuthenticationMode.None => ValidateOptionsResult.Success,
                _ => ValidateOptionsResult.Fail($"Unsupported authentication mode: {options.Mode}.")
            };

            if (result.Succeeded)
            {
                return CsrfOptions.Validator.Validate(options.Csrf, nameof(options.Csrf));
            }

            return result;
        }
    }
}