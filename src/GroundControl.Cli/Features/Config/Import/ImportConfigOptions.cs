namespace GroundControl.Cli.Features.Config.Import;

internal sealed class ImportConfigOptions
{
    public string? FilePath { get; set; }

    public bool Paste { get; set; }

    public bool Yes { get; set; }
}