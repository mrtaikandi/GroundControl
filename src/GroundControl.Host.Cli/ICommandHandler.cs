namespace GroundControl.Host.Cli;

/// <summary>
/// Defines the contract for a command handler that executes command logic.
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// Handles the command execution asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe during async operations.</param>
    /// <returns>An exit code indicating the result of the command execution.</returns>
    Task<int> HandleAsync(CancellationToken cancellationToken);
}