using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace GroundControl.E2E.Tests.Infrastructure;

/// <summary>
/// Invokes the GroundControl CLI as a child process via 'dotnet run'.
/// </summary>
public sealed class CliRunner
{
    private readonly string _apiBaseUrl;
    private readonly string _cliProjectPath;
    private readonly TimeSpan _timeout;

    [SuppressMessage("Design", "CA1054:URI-like properties should not be strings", Justification = "String URL is the design contract for consumer convenience")]
    public CliRunner(string apiBaseUrl, TimeSpan? timeout = null)
    {
        _apiBaseUrl = apiBaseUrl;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);

        // Resolve CLI project path relative to the repository root.
        // The test assembly is at: artifacts/bin/GroundControl.E2E.Tests/<config>/<tfm>/
        // Repository root is 5 levels up from the assembly location.
        var assemblyDir = Path.GetDirectoryName(typeof(CliRunner).Assembly.Location)!;
        var repoRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", ".."));
        _cliProjectPath = Path.Combine(repoRoot, "src", "GroundControl.Cli", "GroundControl.Cli.csproj");

        if (!File.Exists(_cliProjectPath))
        {
            throw new FileNotFoundException(
                $"CLI project not found at {_cliProjectPath}. Check the relative path from test assembly.");
        }
    }

    /// <summary>
    /// Runs a CLI command and returns the result.
    /// Automatically appends --output json --no-interactive and sets the server URL env var.
    /// </summary>
    /// <param name="args">The CLI arguments (e.g., "projects", "create", "--name", "MyProject").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<CliResult> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var fullArgs = BuildArguments(args);
        var arguments = $"run --project \"{_cliProjectPath}\" -- {fullArgs}";

        var psi = new ProcessStartInfo("dotnet", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            Environment =
            {
                ["GroundControl__ServerUrl"] = _apiBaseUrl
            }
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start CLI process");

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdoutBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderrBuilder.AppendLine(e.Data);
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"CLI command timed out after {_timeout.TotalSeconds}s. Args: {fullArgs}");
        }

        return new CliResult(process.ExitCode, stdoutBuilder.ToString().Trim(), stderrBuilder.ToString().Trim());
    }

    /// <summary>
    /// Convenience overload that takes inline args.
    /// </summary>
    public Task<CliResult> RunAsync(CancellationToken cancellationToken, params string[] args) =>
        RunAsync(args, cancellationToken);

    private static string BuildArguments(string[] args)
    {
        // Always append --output json and --no-interactive for deterministic, parseable output
        var allArgs = new List<string>(args) { "--output", "json", "--no-interactive" };
        return string.Join(' ', allArgs.Select(QuoteIfNeeded));
    }

    private static string QuoteIfNeeded(string arg) =>
        arg.Contains(' ', StringComparison.Ordinal) ? $"\"{arg}\"" : arg;
}