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
    public string AppName { get; set; } = string.Empty;

    public int MaxRetries { get; set; }

    public bool DarkMode { get; set; }
}