using System.CommandLine;
using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.ConfigEntries.Create;

internal sealed class CreateConfigEntryCommand : Command<CreateConfigEntryHandler, CreateConfigEntryOptions>
{
    public CreateConfigEntryCommand()
        : base("create", "Create a new configuration entry")
    {
        var keyOption = new Option<string?>("--key", "The configuration key (e.g., Database:ConnectionString)");
        var ownerIdOption = new Option<Guid?>("--owner-id", "The owning template or project ID");
        var ownerTypeOption = new Option<ConfigEntryOwnerType?>("--owner-type", "The owner type (Template or Project)");
        var valueTypeOption = new Option<string?>("--value-type", "The value type name (e.g., String, Int32, Boolean)");
        var sensitiveOption = new Option<bool?>("--sensitive", "Whether the entry contains sensitive data");
        var descriptionOption = new Option<string?>("--description", "The entry description");
        var valueOption = new Option<string[]?>("--value", "Scoped value (e.g., \"default=myval\" or \"env:prod=prodval\"). Repeatable.")
        {
            AllowMultipleArgumentsPerToken = false
        };
        var valuesJsonOption = new Option<string?>("--values-json", "Scoped values as JSON array (e.g., [{\"scopes\":{\"env\":\"prod\"},\"value\":\"prodval\"}])");

        Options.Add(keyOption);
        Options.Add(ownerIdOption);
        Options.Add(ownerTypeOption);
        Options.Add(valueTypeOption);
        Options.Add(sensitiveOption);
        Options.Add(descriptionOption);
        Options.Add(valueOption);
        Options.Add(valuesJsonOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Key = parseResult.GetValue(keyOption);
            options.OwnerId = parseResult.GetValue(ownerIdOption);
            options.OwnerType = parseResult.GetValue(ownerTypeOption);
            options.ValueType = parseResult.GetValue(valueTypeOption);
            options.Sensitive = parseResult.GetValue(sensitiveOption);
            options.Description = parseResult.GetValue(descriptionOption);
            options.Values = parseResult.GetValue(valueOption);
            options.ValuesJson = parseResult.GetValue(valuesJsonOption);
        });
    }
}