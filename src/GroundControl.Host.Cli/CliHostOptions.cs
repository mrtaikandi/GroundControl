using System.CommandLine;

namespace GroundControl.Host.Cli;

/// <summary>
/// Represents configuration options for the CLI host.
/// </summary>
public class CliHostOptions
{
    /// <summary>
    /// Gets the option that enables debug logging to the console.
    /// Use <c>--debug</c> for Information-level logging or <c>--debug verbose</c> for Debug-level logging.
    /// </summary>
    public static readonly Option<string?> DebugOption = CreateDebugOption();

    private static Option<string?> CreateDebugOption()
    {
        var option = new Option<string?>("--debug")
        {
            Description = "Enable debug logging to the console. Use '--debug' for standard or '--debug verbose' or '--debug v' for detailed output.",
            Required = false,
            Arity = ArgumentArity.ZeroOrOne,
            Recursive = true
        };

        option.AcceptOnlyFromAmong("verbose", "v");
        return option;
    }

    /// <summary>
    /// Gets or sets the package server URL used to resolve NuGet packages.
    /// </summary>
    public string PackageServer { get; set; } = "https://packages.capitalontap.com/v3/package";
}