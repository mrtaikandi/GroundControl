namespace GroundControl.Cli.Features.Scopes.Update;

internal sealed class UpdateScopeOptions
{
    public Guid Id { get; set; }

    public string? Dimension { get; set; }

    public string? Values { get; set; }

    public string? Description { get; set; }

    public long? Version { get; set; }
}