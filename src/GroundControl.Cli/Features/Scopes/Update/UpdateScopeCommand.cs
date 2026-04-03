using System.CommandLine;

namespace GroundControl.Cli.Features.Scopes.Update;

internal sealed class UpdateScopeCommand : Command<UpdateScopeHandler, UpdateScopeOptions>
{
    public UpdateScopeCommand()
        : base("update", "Update a scope")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The scope ID" };
        var dimensionOption = new Option<string?>("--dimension") { Description = "The new dimension name" };
        var valuesOption = new Option<string?>("--values") { Description = "Comma-separated allowed values" };
        var descriptionOption = new Option<string?>("--description") { Description = "The new description" };
        var versionOption = new Option<long?>("--version") { Description = "The expected version for optimistic concurrency" };

        Arguments.Add(idArgument);
        Options.Add(dimensionOption);
        Options.Add(valuesOption);
        Options.Add(descriptionOption);
        Options.Add(versionOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
            options.Dimension = parseResult.GetValue(dimensionOption);
            options.Values = parseResult.GetValue(valuesOption);
            options.Description = parseResult.GetValue(descriptionOption);
            options.Version = parseResult.GetValue(versionOption);
        });
    }
}