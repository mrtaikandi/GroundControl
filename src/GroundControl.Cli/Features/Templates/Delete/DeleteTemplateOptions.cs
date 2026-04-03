namespace GroundControl.Cli.Features.Templates.Delete;

internal sealed class DeleteTemplateOptions
{
    public Guid Id { get; set; }

    public long? Version { get; set; }

    public bool Yes { get; set; }
}