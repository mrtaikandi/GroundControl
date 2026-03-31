using System.CommandLine.Parsing;

namespace GroundControl.Host.Cli.Validators;

/// <summary>
/// Provides common validation methods for file-related command-line options.
/// </summary>
public static class FileValidators
{
    /// <summary>
    /// Creates a validator that rejects null, empty, or whitespace-only string values.
    /// </summary>
    /// <param name="error">The error message to display when validation fails.</param>
    /// <returns>An <see cref="Action{OptionResult}"/> that can be added to an option's Validators collection.</returns>
    public static Action<OptionResult> NotNullOrWhiteSpace(string error) => result =>
    {
        var value = result.GetValueOrDefault<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            result.AddError(error);
        }
    };

    /// <summary>
    /// Creates a validator that ensures a file exists at the specified path.
    /// </summary>
    /// <param name="error">The error message to display when the file does not exist.</param>
    /// <returns>An <see cref="Action{OptionResult}"/> that can be added to an option's Validators collection.</returns>
    public static Action<OptionResult> FileExists(string error) => result =>
    {
        var value = result.GetValueOrDefault<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            result.AddError("The file path cannot be empty.");
            return;
        }

        if (!File.Exists(value))
        {
            result.AddError(error);
        }
    };

    /// <summary>
    /// Creates a validator that ensures a file exists at the specified path.
    /// </summary>
    /// <param name="error">The error message to display when the file does not exist.</param>
    /// <returns>An <see cref="Action{OptionResult}"/> that can be added to an option's Validators collection.</returns>
    public static Action<OptionResult> FileInfoExists(string error) => result =>
    {
        var value = result.GetValueOrDefault<FileInfo?>();
        if (value is null)
        {
            result.AddError("The file path cannot be empty.");
            return;
        }

        if (!value.Exists)
        {
            result.AddError(error);
        }
    };
}