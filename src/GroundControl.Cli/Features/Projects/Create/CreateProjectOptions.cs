namespace GroundControl.Cli.Features.Projects.Create;

internal sealed class CreateProjectOptions
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public Guid? GroupId { get; set; }

    public string? TemplateIds { get; set; }
}