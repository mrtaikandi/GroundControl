using System.CommandLine;

namespace GroundControl.Cli.Features.ConfigEntries.Update;

internal sealed class UpdateConfigEntryCommand : Command<UpdateConfigEntryHandler, UpdateConfigEntryOptions>
{
    public UpdateConfigEntryCommand()
        : base("update", "Update a configuration entry")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The configuration entry ID" };
        var valueTypeOption = new Option<string?>("--value-type", "The new value type name");
        var sensitiveOption = new Option<bool?>("--sensitive", "Whether the entry contains sensitive data");
        var descriptionOption = new Option<string?>("--description", "The new description");
        var valueOption = new Option<string[]?>("--value", "Scoped value (e.g., \"default=myval\" or \"env:prod=prodval\"). Repeatable.")
        {
            AllowMultipleArgumentsPerToken = false
        };
        var valuesJsonOption = new Option<string?>("--values-json", "Scoped values as JSON array");
        var versionOption = new Option<long?>("--version", "The expected version for optimistic concurrency");

        Arguments.Add(idArgument);
        Options.Add(valueTypeOption);
        Options.Add(sensitiveOption);
        Options.Add(descriptionOption);
        Options.Add(valueOption);
        Options.Add(valuesJsonOption);
        Options.Add(versionOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
            options.ValueType = parseResult.GetValue(valueTypeOption);
            options.Sensitive = parseResult.GetValue(sensitiveOption);
            options.Description = parseResult.GetValue(descriptionOption);
            options.Values = parseResult.GetValue(valueOption);
            options.ValuesJson = parseResult.GetValue(valuesJsonOption);
            options.Version = parseResult.GetValue(versionOption);
        });
    }
}