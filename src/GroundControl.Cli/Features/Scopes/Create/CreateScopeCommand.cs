using System.CommandLine;

namespace GroundControl.Cli.Features.Scopes.Create;

internal sealed class CreateScopeCommand : Command<CreateScopeHandler, CreateScopeOptions>
{
    public CreateScopeCommand()
        : base("create", "Create a new scope")
    {
        var dimensionOption = new Option<string?>("--dimension", "The scope dimension name (e.g., Environment, Region)");
        var valuesOption = new Option<string?>("--values", "Comma-separated allowed values (e.g., dev,staging,prod)");
        var descriptionOption = new Option<string?>("--description", "The scope description");

        Options.Add(dimensionOption);
        Options.Add(valuesOption);
        Options.Add(descriptionOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Dimension = parseResult.GetValue(dimensionOption);
            options.Values = parseResult.GetValue(valuesOption);
            options.Description = parseResult.GetValue(descriptionOption);
        });
    }
}