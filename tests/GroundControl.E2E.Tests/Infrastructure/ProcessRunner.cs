using System.Diagnostics;
using System.Text;

namespace GroundControl.E2E.Tests.Infrastructure;

/// <summary>
/// Unified process execution for E2E tests. Runs a process to completion,
/// streaming stdout/stderr lines to xUnit's test output and returning the accumulated result.
/// </summary>
public static class ProcessRunner
{
    /// <summary>
    /// Runs a process and returns the accumulated result.
    /// Output lines are streamed to <see cref="TestContext.Current"/> as they arrive.
    /// </summary>
    public static async Task<ProcessResult> RunAsync(ProcessRunOptions options, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo(options.FileName, options.Arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        if (options.WorkingDirectory is not null)
        {
            psi.WorkingDirectory = options.WorkingDirectory;
        }

        if (options.EnvironmentVariables is not null)
        {
            foreach (var (key, value) in options.EnvironmentVariables)
            {
                psi.Environment[key] = value;
            }
        }

        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException($"Failed to start process '{options.FileName}'");

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        var stdoutWriter = new TestOutputWriter();
        var stderrWriter = new TestOutputWriter();

        using var cts = options.Timeout.HasValue
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;

        var effectiveToken = cts?.Token ?? cancellationToken;

        if (options.Timeout.HasValue && cts is not null)
        {
            cts.CancelAfter(options.Timeout.Value);
        }

        var readStdout = ReadLinesAsync(process.StandardOutput, stdoutBuilder, stdoutWriter, effectiveToken);
        var readStderr = ReadLinesAsync(process.StandardError, stderrBuilder, stderrWriter, effectiveToken);

        try
        {
            await process.WaitForExitAsync(effectiveToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (options.Timeout.HasValue && !cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);

            // Allow read tasks to finish capturing partial output
            await Task.WhenAll(readStdout, readStderr).ConfigureAwait(false);

            throw new TimeoutException(
                $"Process '{options.FileName}' timed out after {options.Timeout.Value.TotalSeconds}s.{Environment.NewLine}" +
                $"Arguments: {options.Arguments}{Environment.NewLine}" +
                $"--------------------------- STD OUT ---------------------------{Environment.NewLine}" +
                $"{stdoutBuilder}{Environment.NewLine}" +
                $"--------------------------- STD ERR ---------------------------{Environment.NewLine}" +
                $"{stderrBuilder}{Environment.NewLine}" +
                $"---------------------------------------------------------------");
        }

        // Process exited normally — wait for pipe readers to drain remaining buffered output
        await Task.WhenAll(readStdout, readStderr).ConfigureAwait(false);

        return new ProcessResult(process.ExitCode, stdoutBuilder.ToString().TrimEnd(), stderrBuilder.ToString().TrimEnd());
    }

    private static async Task ReadLinesAsync(StreamReader reader, StringBuilder accumulator, TestOutputWriter writer, CancellationToken cancellationToken)
    {
        try
        {
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                accumulator.AppendLine(line);
                writer.WriteLine(line);
            }
        }
        catch (OperationCanceledException)
        {
            // Process was killed due to timeout or cancellation.
            // Partial output is already in the accumulator.
        }
    }
}