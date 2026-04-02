namespace GroundControl.Cli.Features.Config.Import;

/// <summary>
/// Options for the <c>config import</c> command.
/// </summary>
internal sealed class ImportConfigOptions
{
    /// <summary>
    /// Gets or sets the path to a JSON configuration file to import.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to read JSON from interactive paste input.
    /// </summary>
    public bool Paste { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to skip the confirmation prompt.
    /// </summary>
    public bool Yes { get; set; }
}