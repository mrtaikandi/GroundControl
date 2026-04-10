using System.Diagnostics.CodeAnalysis;

namespace GroundControl.Link.Internals.Connection;

/// <summary>
/// Shared static helpers used by connection strategies and the configuration provider.
/// </summary>
internal static class ConnectionHelpers
{
    /// <summary>
    /// Adds 75-125% jitter to a base delay, with a minimum of 100ms.
    /// </summary>
    [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Jitter for polling/reconnect intervals does not require cryptographic randomness")]
    public static TimeSpan AddJitter(TimeSpan baseDelay)
    {
        var jitterFactor = 0.75 + (Random.Shared.NextDouble() * 0.5);
        return TimeSpan.FromMilliseconds(Math.Max(baseDelay.TotalMilliseconds * jitterFactor, 100));
    }
}