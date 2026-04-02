using System.CommandLine;

namespace GroundControl.Cli.Features.Config.Import;

internal sealed class ImportConfigCommand : Command<ImportConfigHandler, ImportConfigOptions>
{
    private static readonly Option<string?> FileOption = new("--file", "Path to a JSON configuration file");

    private static readonly Option<bool> PasteOption = new("--paste", "Paste JSON configuration interactively");

    private static readonly Option<bool> YesOption = new("--yes", "Skip confirmation prompt");

    public ImportConfigCommand()
        : base("import", "Import server configuration from a JSON file or paste")
    {
        Options.Add(FileOption);
        Options.Add(PasteOption);
        Options.Add(YesOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.FilePath = parseResult.GetValue(FileOption);
            options.Paste = parseResult.GetValue(PasteOption);
            options.Yes = parseResult.GetValue(YesOption);
        });

        Validators.Add(result =>
        {
            var hasFile = result.GetValue(FileOption) is not null;
            var hasPaste = result.GetValue(PasteOption);

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