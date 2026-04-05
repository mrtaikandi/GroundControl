using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace GroundControl.E2E.Tests.Infrastructure;

/// <summary>
/// Invokes the GroundControl CLI as a child process via 'dotnet run'.
/// </summary>
public sealed class CliRunner
{
    private static readonly string CliProjectPath = ResolveCliProjectPath();

    private readonly string _apiBaseUrl;
    private readonly TimeSpan _timeout;

    [SuppressMessage("Design", "CA1054:URI-like properties should not be strings", Justification = "String URL is the design contract for consumer convenience")]
    public CliRunner(string apiBaseUrl, TimeSpan? timeout = null)
    {
        _apiBaseUrl = apiBaseUrl;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Builds the CLI project once so that subsequent RunAsync calls can use --no-build.
    /// Call this from an assembly-level fixture before any tests execute.
    /// </summary>
    public static async Task BuildAsync(CancellationToken cancellationToken = default)
    {
        var options = new ProcessRunOptions
        {
            FileName = "dotnet",
            Arguments = $"build \"{CliProjectPath}\" -c Debug --nologo -v q"
        };

        var result = await ProcessRunner.RunAsync(options, cancellationToken).ConfigureAwait(false);
        result.ThrowIfFailed($"dotnet build failed for {CliProjectPath}");
    }

    private static string ResolveCliProjectPath()
    {
        var repoRoot = typeof(CliRunner).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "RepositoryRoot")?.Value
                       ?? throw new InvalidOperationException("RepositoryRoot assembly metadata not found.");

        var path = Path.Combine(repoRoot, "src", "GroundControl.Cli", "GroundControl.Cli.csproj");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"CLI project not found at {path}. Check the relative path from test assembly.");
        }

        return path;
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
        var arguments = $"run --no-build --project \"{CliProjectPath}\" -- {fullArgs}";

        var options = new ProcessRunOptions
        {
            FileName = "dotnet",
            Arguments = arguments,
            Timeout = _timeout,
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["GroundControl__ServerUrl"] = _apiBaseUrl
            }
        };

        var result = await ProcessRunner.RunAsync(options, cancellationToken).ConfigureAwait(false);
        return new CliResult(result.ExitCode, result.Stdout, result.Stderr);
    }

    /// <summary>
    /// Convenience overload that takes inline args.
    /// </summary>
    public Task<CliResult> RunAsync(CancellationToken cancellationToken, params string[] args) =>
        RunAsync(args, cancellationToken);

    private static string BuildArguments(string[] args)
    {
        // Always append --output json and --no-interactive for deterministic, parseable output
        var allArgs = new List<string>(args) { "--output", "json", "--no-interactive", "--debug", "verbose" };
        return string.Join(' ', allArgs.Select(QuoteIfNeeded));
    }

    private static string QuoteIfNeeded(string arg) =>
        arg.Contains(' ', StringComparison.Ordinal) ? $"\"{arg}\"" : arg;
}