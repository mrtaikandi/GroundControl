using System.CommandLine;

namespace GroundControl.Cli.Features.Config.Import;

internal sealed class ImportConfigCommand : Command<ImportConfigHandler, ImportConfigOptions>
{
    public ImportConfigCommand()
        : base("import", "Import server configuration from a JSON file or paste")
    {
        var fileOption = new Option<string?>("--file", "Path to a JSON configuration file");
        var pasteOption = new Option<bool>("--paste", "Paste JSON configuration interactively");
        var yesOption = new Option<bool>("--yes", "Skip confirmation prompt");

        Options.Add(fileOption);
        Options.Add(pasteOption);
        Options.Add(yesOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.FilePath = parseResult.GetValue(fileOption);
            options.Paste = parseResult.GetValue(pasteOption);
            options.Yes = parseResult.GetValue(yesOption);
        });

        Validators.Add(result =>
        {
            var hasFile = result.GetValue(fileOption) is not null;
            var hasPaste = result.GetValue(pasteOption);

            if (!hasFile && !hasPaste)
            {
                result.AddError("Specify either --file <path> or --paste.");
            }

            if (hasFile && hasPaste)
            {
                result.AddError("Cannot use both --file and --paste at the same time.");
            }
        });
    }
}