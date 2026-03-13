using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Tests.Infrastructure.Extensions;

internal static class HttpResponseMessageExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    extension(HttpResponseMessage response)
    {
        public async Task<ProblemDetails?> ReadProblemAsync(CancellationToken cancellationToken = default) =>
            await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions, cancellationToken).ConfigureAwait(false);

        public async Task<HttpValidationProblemDetails?> ReadValidationProblemAsync(CancellationToken cancellationToken = default) =>
            await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}