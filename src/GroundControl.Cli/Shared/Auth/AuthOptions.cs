namespace GroundControl.Cli.Shared.Auth;

/// <summary>
/// Configuration options for authentication, bound to the <c>GroundControl:Auth</c> section.
/// </summary>
internal sealed class AuthOptions
{
    /// <summary>
    /// Gets or sets the authentication method (e.g., "Bearer", "ApiKey", "Credentials").
    /// </summary>
    public string? Method { get; set; }

    /// <summary>
    /// Gets or sets the personal access token for Bearer authentication.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Gets or sets the client identifier for API key authentication.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client secret for API key authentication.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the username for credentials authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the password for credentials authentication.
    /// </summary>
    public string? Password { get; set; }
}