using System.CommandLine;
using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Variables.Create;

internal sealed class CreateVariableCommand : Command<CreateVariableHandler, CreateVariableOptions>
{
    public CreateVariableCommand()
        : base("create", "Create a new variable")
    {
        var nameOption = new Option<string?>("--name", "The variable name");
        var scopeOption = new Option<VariableScope?>("--scope", "The variable scope (Global or Project)");
        var groupIdOption = new Option<Guid?>("--group-id", "The group ID (for Global scope)");
        var projectIdOption = new Option<Guid?>("--project-id", "The project ID (for Project scope)");
        var sensitiveOption = new Option<bool?>("--sensitive", "Whether the variable contains sensitive data");
        var descriptionOption = new Option<string?>("--description", "The variable description");
        var valueOption = new Option<string[]?>("--value", "Scoped value (e.g., \"default=myval\" or \"env:prod=prodval\"). Repeatable.")
        {
            AllowMultipleArgumentsPerToken = false
        };
        var valuesJsonOption = new Option<string?>("--values-json", "Scoped values as JSON array");

        Options.Add(nameOption);
        Options.Add(scopeOption);
        Options.Add(groupIdOption);
        Options.Add(projectIdOption);
        Options.Add(sensitiveOption);
        Options.Add(descriptionOption);
        Options.Add(valueOption);
        Options.Add(valuesJsonOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Name = parseResult.GetValue(nameOption);
            options.Scope = parseResult.GetValue(scopeOption);
            options.GroupId = parseResult.GetValue(groupIdOption);
            options.ProjectId = parseResult.GetValue(projectIdOption);
            options.Sensitive = parseResult.GetValue(sensitiveOption);
            options.Description = parseResult.GetValue(descriptionOption);
            options.Values = parseResult.GetValue(valueOption);
            options.ValuesJson = parseResult.GetValue(valuesJsonOption);
        });
    }
}