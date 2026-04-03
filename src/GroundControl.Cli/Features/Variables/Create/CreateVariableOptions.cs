using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Variables.Create;

internal sealed class CreateVariableOptions
{
    public string? Name { get; set; }

    public VariableScope? Scope { get; set; }

    public Guid? GroupId { get; set; }

    public Guid? ProjectId { get; set; }

    public bool? Sensitive { get; set; }

    public string? Description { get; set; }

    public string[]? Values { get; set; }

    public string? ValuesJson { get; set; }
}