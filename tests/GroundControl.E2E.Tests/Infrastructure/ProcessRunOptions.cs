namespace GroundControl.E2E.Tests.Infrastructure;

/// <summary>
/// Options for running a process via <see cref="ProcessRunner"/>.
/// </summary>
public sealed record ProcessRunOptions
{
    /// <summary>
    /// Gets the executable file name (e.g. "dotnet", "docker").
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Gets the command-line arguments.
    /// </summary>
    public required string Arguments { get; init; }

    /// <summary>
    /// Gets the working directory for the process, or null to use the current directory.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Gets additional environment variables to set on the process.
    /// </summary>
    public IDictionary<string, string?>? EnvironmentVariables { get; init; }

    /// <summary>
    /// Gets the timeout after which the process tree is killed and a <see cref="TimeoutException"/> is thrown.
    /// Null means no timeout.
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}