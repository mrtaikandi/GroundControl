namespace GroundControl.Api.Tests.Infrastructure;

/// <summary>
/// Polls a condition with short intervals instead of using fixed-duration delays.
/// </summary>
internal static class TestWaiter
{
    /// <summary>
    /// Waits until the specified condition returns <c>true</c>, polling at regular intervals.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="timeout">Maximum time to wait. Defaults to 5 seconds.</param>
    /// <param name="pollInterval">Interval between polls. Defaults to 25 milliseconds.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task WaitUntilAsync(
        Func<bool> condition,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        pollInterval ??= TimeSpan.FromMilliseconds(25);
        var deadline = DateTime.UtcNow + timeout.Value;

        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(pollInterval.Value, cancellationToken).ConfigureAwait(false);
        }
    }
}