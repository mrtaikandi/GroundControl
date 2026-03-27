namespace GroundControl.Api.Shared.Security;

/// <summary>
/// Configuration options for CSRF protection.
/// </summary>
internal sealed class CsrfOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether CSRF protection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the name of the CSRF cookie.
    /// </summary>
    public string CookieName { get; set; } = "XSRF-TOKEN";

    /// <summary>
    /// Gets or sets the name of the CSRF header.
    /// </summary>
    public string HeaderName { get; set; } = "X-XSRF-TOKEN";
}