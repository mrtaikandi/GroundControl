namespace GroundControl.E2E.Tests.Infrastructure;

/// <summary>
/// Captures the result of a process execution.
/// </summary>
public sealed record ProcessResult(int ExitCode, string Stdout, string Stderr)
{
    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if the process exited with a non-zero code.
    /// </summary>
    /// <param name="contextMessage">Optional context to include in the error message (e.g. "dotnet build").</param>
    public void ThrowIfFailed(string? contextMessage = null)
    {
        if (ExitCode == 0)
        {
            return;
        }

        var message = string.IsNullOrEmpty(contextMessage)
            ? $"Process exited with code {ExitCode}."
            : $"{contextMessage} (exit code {ExitCode}).";

        throw new InvalidOperationException(
            $"{message}{Environment.NewLine}" +
            $"--------------------------- STD ERR ---------------------------{Environment.NewLine}" +
            $"{Stderr}{Environment.NewLine}" +
            $"---------------------------------------------------------------");
    }
}