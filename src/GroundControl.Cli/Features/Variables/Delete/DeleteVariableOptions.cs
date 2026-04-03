namespace GroundControl.Cli.Features.Variables.Delete;

internal sealed class DeleteVariableOptions
{
    public Guid Id { get; set; }

    public long? Version { get; set; }

    public bool Yes { get; set; }
}