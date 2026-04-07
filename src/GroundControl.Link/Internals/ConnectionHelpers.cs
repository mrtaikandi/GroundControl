using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace GroundControl.Link.Internals;

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

    /// <summary>
    /// Parses SSE event JSON to extract flattened config data and snapshot version.
    /// </summary>
    public static (Dictionary<string, string> Config, string? SnapshotVersion) ParseConfigDataWithVersion(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (doc.RootElement.TryGetProperty("data", out var data))
        {
            DefaultConfigFetcher.FlattenElement(data, string.Empty, config);
        }

        string? snapshotVersion = null;
        if (doc.RootElement.TryGetProperty("snapshotVersion", out var version))
        {
            snapshotVersion = version.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return (config, snapshotVersion);
    }
}