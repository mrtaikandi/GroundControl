namespace GroundControl.Cli.Features.ConfigEntries.Get;

internal sealed class GetConfigEntryOptions
{
    public Guid Id { get; set; }

    public bool? Decrypt { get; set; }
}