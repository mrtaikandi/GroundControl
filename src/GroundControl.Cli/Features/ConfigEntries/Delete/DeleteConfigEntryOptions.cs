namespace GroundControl.Cli.Features.ConfigEntries.Delete;

internal sealed class DeleteConfigEntryOptions
{
    public Guid Id { get; set; }

    public long? Version { get; set; }

    public bool Yes { get; set; }
}