using System.ComponentModel.DataAnnotations;

namespace GroundControl.Cli.Shared.ApiClient;

/// <summary>
/// Configuration options for the GroundControl API client.
/// </summary>
/// <remarks>Binds to the <c>GroundControl</c> configuration section.</remarks>
internal sealed class GroundControlClientOptions
{
    /// <summary>
    /// Gets or sets the base URL of the GroundControl server.
    /// </summary>
    [Required]
    public string ServerUrl { get; set; } = string.Empty;
}