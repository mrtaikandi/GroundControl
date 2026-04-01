namespace GroundControl.Host.Cli;

/// <summary>
/// Specifies the output format for CLI command results.
/// </summary>
public enum OutputFormat
{
    /// <summary>
    /// Renders output as a human-readable Spectre.Console table.
    /// </summary>
    Table,

    /// <summary>
    /// Renders output as pretty-printed JSON.
    /// </summary>
    Json
}