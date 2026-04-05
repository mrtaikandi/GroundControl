using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Shouldly;

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
    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    public T ParseOutput<T>() where T : class
    {
        T? result = null;
        Exception? exception = null;

        try
        {
            result = JsonSerializer.Deserialize<T>(Stdout, JsonOptions);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        return result is not null && exception is null
            ? result
            : throw new InvalidOperationException($"Failed to deserialize CLI output to {typeof(T).Name}. Stdout: {Stdout}", exception);
    }

    /// <summary>
    /// Asserts that the CLI exited with code 0. Throws with stderr on failure.
    /// </summary>
    public void ShouldSucceed()
    {
        ExitCode.ShouldBe(0, $"CLI failed.\nStdout: {Stdout}\nStderr: {Stderr}");
    }
}