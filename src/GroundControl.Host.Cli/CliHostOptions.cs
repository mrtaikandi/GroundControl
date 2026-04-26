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

    /// <summary>
    /// Gets the option that controls output format (table or json).
    /// </summary>
    public static readonly Option<OutputFormat> OutputOption = CreateOutputOption();

    /// <summary>
    /// Gets the option that disables interactive prompts.
    /// </summary>
    public static readonly Option<bool> NoInteractiveOption = new("--no-interactive")
    {
        Description = "Disable interactive prompts and use defaults.",
        Recursive = true
    };

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

    private static Option<OutputFormat> CreateOutputOption() => new("--output")
    {
        Description = "Output format: table or json.",
        Recursive = true
    };

    /// <summary>
    /// Gets or sets the package server URL used to resolve NuGet packages.
    /// </summary>
    public string PackageServer { get; set; } = "https://packages.capitalontap.com/v3/package";

    /// <summary>
    /// Gets or sets the output format for command results.
    /// </summary>
    public OutputFormat OutputFormat { get; set; } = OutputFormat.Table;

    /// <summary>
    /// Gets or sets a value indicating whether interactive prompts are disabled.
    /// </summary>
    public bool NoInteractive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <c>--debug</c> was specified for the current invocation.
    /// </summary>
    /// <remarks>True when the user passed <c>--debug</c> at any level (with or without a verbosity value).</remarks>
    public bool Debug { get; set; }
}