namespace GroundControl.Cli.Features.Variables.Get;

internal sealed class GetVariableOptions
{
    public Guid Id { get; set; }

    public bool? Decrypt { get; set; }
}