namespace GroundControl.Link.Internals.Connection;

/// <summary>
/// Strategy for background configuration refresh based on <see cref="ConnectionMode"/>.
/// </summary>
internal interface IConnectionStrategy
{
    /// <summary>
    /// Runs the background connection loop until cancellation.
    /// </summary>
    Task ExecuteAsync(GroundControlStore store, CancellationToken cancellationToken);
}