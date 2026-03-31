namespace GroundControl.Api.Core.Validation;

/// <summary>
/// Provides contextual information to validators beyond the request body.
/// </summary>
internal readonly struct ValidationContext
{
    /// <summary>
    /// Gets the HTTP context for the current request.
    /// </summary>
    public HttpContext HttpContext { get; init; }
}