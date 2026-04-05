using System.Text.Json;

namespace GroundControl.E2E.Tests.Infrastructure;

/// <summary>
/// Captures the result of a CLI process invocation.
/// </summary>
public sealed record CliResult(int ExitCode, string Stdout, string Stderr)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Deserializes the stdout as JSON into the specified type.
    /// </summary>
    public T ParseOutput<T>()
    {
        var result = JsonSerializer.Deserialize<T>(Stdout, JsonOptions);
        return result ?? throw new InvalidOperationException(
            $"Failed to deserialize CLI output to {typeof(T).Name}. Stdout: {Stdout}");
    }

    /// <summary>
    /// Asserts that the CLI exited with code 0. Throws with stderr on failure.
    /// </summary>
    public void ShouldSucceed()
    {
        if (ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"CLI exited with code {ExitCode}.\nStdout: {Stdout}\nStderr: {Stderr}");
        }
    }
}