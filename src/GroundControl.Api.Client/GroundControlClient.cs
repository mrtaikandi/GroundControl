using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GroundControl.Api.Client;

public partial class GroundControlClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
    {
        settings.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        settings.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    }

    partial void ProcessResponse(HttpClient client, HttpResponseMessage response)
    {
        // The OpenAPI spec defines only "200" as success, but the API returns
        // 201 (Created) and 204 (No Content). Normalize all 2xx responses so
        // the generated status-code check does not throw on success.
        var statusCode = (int)response.StatusCode;
        if (statusCode is >= 200 and < 300)
        {
            response.StatusCode = HttpStatusCode.OK;
        }
    }
}