namespace GroundControl.Cli.Features.Templates.Create;

internal sealed class CreateTemplateOptions
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public Guid? GroupId { get; set; }
}