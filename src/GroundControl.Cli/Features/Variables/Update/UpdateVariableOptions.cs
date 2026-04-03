namespace GroundControl.Cli.Features.Variables.Update;

internal sealed class UpdateVariableOptions
{
    public Guid Id { get; set; }

    public bool? Sensitive { get; set; }

    public string? Description { get; set; }

    public string[]? Values { get; set; }

    public string? ValuesJson { get; set; }

    public long? Version { get; set; }
}