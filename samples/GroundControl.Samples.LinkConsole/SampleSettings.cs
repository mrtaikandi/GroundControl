namespace GroundControl.Samples.LinkConsole;

/// <summary>
/// Strongly-typed settings populated from GroundControl configuration.
/// </summary>
/// <remarks>
/// These properties map to configuration entries you create in the GroundControl server.
/// For example, a configuration entry with key <c>Sample:AppName</c> maps to <see cref="AppName"/>.
/// </remarks>
internal sealed class SampleSettings
{
    /// <summary>
    /// Gets or sets the application name.
    /// </summary>
    public string AppName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of retries.
    /// </summary>
    public int MaxRetries { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether dark mode is enabled.
    /// </summary>
    public bool DarkMode { get; set; }
}