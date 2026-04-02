using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GroundControl.Cli.Shared.ErrorHandling;

internal sealed partial class ProblemDetailsDelegatingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var problemDetails = await TryParseProblemDetailsAsync(response, cancellationToken).ConfigureAwait(false);
        if (problemDetails is not null)
        {
            throw new ProblemDetailsApiException(
                (int)response.StatusCode,
                problemDetails.Title,
                problemDetails.Detail,
                problemDetails.Errors);
        }

        throw new ProblemDetailsApiException(
            (int)response.StatusCode,
            response.ReasonPhrase,
            null);
    }

    private static async Task<ProblemDetailsResponse?> TryParseProblemDetailsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentType?.MediaType is not ("application/problem+json" or "application/json"))
        {
            return null;
        }

        try
        {
            return await response.Content.ReadFromJsonAsync(ProblemDetailsJsonContext.Default.ProblemDetailsResponse, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal sealed class ProblemDetailsResponse
    {
        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("detail")]
        public string? Detail { get; init; }

        [JsonPropertyName("status")]
        public int? Status { get; init; }

        [JsonPropertyName("errors")]
        public Dictionary<string, string[]>? Errors { get; init; }
    }

    [JsonSerializable(typeof(ProblemDetailsResponse))]
    internal sealed partial class ProblemDetailsJsonContext : JsonSerializerContext;
}