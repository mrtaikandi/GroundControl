namespace GroundControl.Cli.Features.ConfigEntries.Update;

internal sealed class UpdateConfigEntryOptions
{
    public Guid Id { get; set; }

    public string? ValueType { get; set; }

    public bool? Sensitive { get; set; }

    public string? Description { get; set; }

    public string[]? Values { get; set; }

    public string? ValuesJson { get; set; }

    public long? Version { get; set; }
}