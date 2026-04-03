namespace GroundControl.Cli.Features.Scopes.Delete;

internal sealed class DeleteScopeOptions
{
    public Guid Id { get; set; }

    public long? Version { get; set; }

    public bool Yes { get; set; }
}