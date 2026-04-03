using System.CommandLine;

namespace GroundControl.Cli.Features.Variables.Get;

internal sealed class GetVariableCommand : Command<GetVariableHandler, GetVariableOptions>
{
    public GetVariableCommand()
        : base("get", "Get a variable by ID")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The variable ID" };
        var decryptOption = new Option<bool?>("--decrypt") { Description = "Decrypt sensitive values" };

        Arguments.Add(idArgument);
        Options.Add(decryptOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
            options.Decrypt = parseResult.GetValue(decryptOption);
        });
    }
}