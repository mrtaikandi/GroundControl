namespace GroundControl.Cli.Features.Projects.Update;

internal sealed class UpdateProjectOptions
{
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public Guid? GroupId { get; set; }

    public string? TemplateIds { get; set; }

    public long? Version { get; set; }
}