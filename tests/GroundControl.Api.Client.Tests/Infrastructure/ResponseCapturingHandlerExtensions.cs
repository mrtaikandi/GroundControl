using System.Text.Json;

namespace GroundControl.Api.Client.Tests.Infrastructure;

internal static class ResponseCapturingHandlerExtensions
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Deserializes the captured response body from the handler's buffered bytes.
    /// Use this instead of reading from <see cref="ResponseCapturingHandler.LastResponse"/>
    /// because NSwag disposes the response after processing.
    /// </summary>
    public static TResponse DeserializeCapturedResponse<TResponse>(this ResponseCapturingHandler handler)
        where TResponse : class
    {
        handler.LastResponseBody.ShouldNotBeNull();
        var result = JsonSerializer.Deserialize<TResponse>(handler.LastResponseBody, SerializerOptions);
        result.ShouldNotBeNull();
        return result;
    }
}