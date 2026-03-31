using System.CommandLine.Parsing;

namespace GroundControl.Host.Cli;

/// <summary>
/// Provides common parsing methods for command-line arguments.
/// </summary>
public static class Parsers
{
    /// <summary>
    /// Parses a single CLI argument token into a <see cref="FileInfo"/> instance.
    /// </summary>
    /// <param name="result">The argument parsing result that contains the input token.</param>
    /// <returns>
    /// A <see cref="FileInfo"/> when parsing succeeds; otherwise, <c>null</c> after recording an error.
    /// </returns>
    public static FileInfo? FileInfoParser(ArgumentResult result)
    {
        var token = result.Tokens.Single().Value.Trim('"');

        try
        {
            // Justification: We intentionally want to allow this, as it gives more flexibility to the user.
            // nosemgrep
            return new FileInfo(token);
        }
        catch (Exception ex)
        {
            result.AddError($"The value '{token}' is not a valid file path. {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses a single CLI argument token into a <see cref="DirectoryInfo"/> instance.
    /// </summary>
    /// <param name="result">The argument parsing result that contains the input token.</param>
    /// <returns>
    /// A <see cref="DirectoryInfo"/> when parsing succeeds; otherwise, <c>null</c> after recording an error.
    /// </returns>
    public static DirectoryInfo? DirectoryInfoParser(ArgumentResult result)
    {
        var token = result.Tokens.Single().Value.Trim('"');

        try
        {
            // Justification: We intentionally want to allow this, as it gives more flexibility to the user.
            // nosemgrep
            return new DirectoryInfo(token);
        }
        catch (Exception ex)
        {
            result.AddError($"The value '{token}' is not a valid file path. {ex.Message}");
            return null;
        }
    }
}