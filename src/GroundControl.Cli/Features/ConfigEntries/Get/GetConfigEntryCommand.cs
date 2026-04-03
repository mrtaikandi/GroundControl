using System.CommandLine;

namespace GroundControl.Cli.Features.ConfigEntries.Get;

internal sealed class GetConfigEntryCommand : Command<GetConfigEntryHandler, GetConfigEntryOptions>
{
    public GetConfigEntryCommand()
        : base("get", "Get a configuration entry by ID")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The configuration entry ID" };
        var decryptOption = new Option<bool?>("--decrypt", "Decrypt sensitive values");

        Arguments.Add(idArgument);
        Options.Add(decryptOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
            options.Decrypt = parseResult.GetValue(decryptOption);
        });
    }
}