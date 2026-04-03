using System.CommandLine;

namespace GroundControl.Cli.Features.Variables.Update;

internal sealed class UpdateVariableCommand : Command<UpdateVariableHandler, UpdateVariableOptions>
{
    public UpdateVariableCommand()
        : base("update", "Update a variable")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The variable ID" };
        var sensitiveOption = new Option<bool?>("--sensitive") { Description = "Whether the variable contains sensitive data" };
        var descriptionOption = new Option<string?>("--description") { Description = "The new description" };
        var valueOption = new Option<string[]?>("--value")
        {
            Description = "Scoped value (e.g., \"default=myval\" or \"env:prod=prodval\"). Repeatable.",
            AllowMultipleArgumentsPerToken = false
        };
        var valuesJsonOption = new Option<string?>("--values-json") { Description = "Scoped values as JSON array" };
        var versionOption = new Option<long?>("--version") { Description = "The expected version for optimistic concurrency" };

        Arguments.Add(idArgument);
        Options.Add(sensitiveOption);
        Options.Add(descriptionOption);
        Options.Add(valueOption);
        Options.Add(valuesJsonOption);
        Options.Add(versionOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
            options.Sensitive = parseResult.GetValue(sensitiveOption);
            options.Description = parseResult.GetValue(descriptionOption);
            options.Values = parseResult.GetValue(valueOption);
            options.ValuesJson = parseResult.GetValue(valuesJsonOption);
            options.Version = parseResult.GetValue(versionOption);
        });
    }
}