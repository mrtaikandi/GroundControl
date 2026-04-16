using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GroundControl.Api.Client;

public partial class GroundControlClient
{
    private static readonly AsyncLocal<string?> PendingIfMatch = new();

    /// <summary>
    /// Sets the If-Match header for the next API request. The value is automatically cleared after use.
    /// </summary>
    /// <param name="version">The entity version to use as the ETag value.</param>
    public static void SetIfMatch(long version) => PendingIfMatch.Value = $"\"{version}\"";

    /// <summary>
    /// Sets the If-Match header for the next API request and returns a scope that guarantees
    /// the pending value is cleared when disposed, even if the request throws before it reaches
    /// the HTTP pipeline.
    /// </summary>
    /// <param name="version">The entity version to use as the ETag value.</param>
    /// <returns>A disposable scope that clears the pending If-Match on dispose.</returns>
    public static IDisposable BeginIfMatchScope(long version)
    {
        SetIfMatch(version);
        return new IfMatchScope();
    }

    private sealed class IfMatchScope : IDisposable
    {
        public void Dispose() => PendingIfMatch.Value = null;
    }

    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
    {
        settings.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        settings.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    }

    partial void PrepareRequest(System.Net.Http.HttpClient client, System.Net.Http.HttpRequestMessage request, string url)
    {
        if (PendingIfMatch.Value is { } ifMatch)
        {
            request.Headers.IfMatch.Add(new EntityTagHeaderValue(ifMatch));
            PendingIfMatch.Value = null;
        }
    }
}