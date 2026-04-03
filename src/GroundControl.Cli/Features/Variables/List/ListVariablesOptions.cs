using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Variables.List;

internal sealed class ListVariablesOptions
{
    public VariableScope? Scope { get; set; }

    public Guid? GroupId { get; set; }

    public Guid? ProjectId { get; set; }

    public bool? Decrypt { get; set; }
}