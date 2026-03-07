namespace GroundControl.Api.Shared.Security;

/// <summary>
/// Defines the authentication mode for the application.
/// </summary>
internal enum AuthenticationMode
{
    /// <summary>
    /// No authentication. All requests are treated as a synthetic admin.
    /// </summary>
    None,

    /// <summary>
    /// Built-in authentication using ASP.NET Identity with MongoDB.
    /// </summary>
    BuiltIn,

    /// <summary>
    /// External authentication via an external identity provider.
    /// </summary>
    External
}