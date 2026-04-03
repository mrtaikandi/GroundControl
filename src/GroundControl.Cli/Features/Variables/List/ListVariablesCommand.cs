using System.CommandLine;
using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Variables.List;

internal sealed class ListVariablesCommand : Command<ListVariablesHandler, ListVariablesOptions>
{
    public ListVariablesCommand()
        : base("list", "List variables")
    {
        var scopeOption = new Option<VariableScope?>("--scope") { Description = "Filter by scope (Global or Project)" };
        var groupIdOption = new Option<Guid?>("--group-id") { Description = "Filter by group ID" };
        var projectIdOption = new Option<Guid?>("--project-id") { Description = "Filter by project ID" };
        var decryptOption = new Option<bool?>("--decrypt") { Description = "Decrypt sensitive values" };

        Options.Add(scopeOption);
        Options.Add(groupIdOption);
        Options.Add(projectIdOption);
        Options.Add(decryptOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Scope = parseResult.GetValue(scopeOption);
            options.GroupId = parseResult.GetValue(groupIdOption);
            options.ProjectId = parseResult.GetValue(projectIdOption);
            options.Decrypt = parseResult.GetValue(decryptOption);
        });
    }
}