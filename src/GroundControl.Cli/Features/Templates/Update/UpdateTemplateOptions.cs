namespace GroundControl.Cli.Features.Templates.Update;

internal sealed class UpdateTemplateOptions
{
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public Guid? GroupId { get; set; }

    public long? Version { get; set; }
}