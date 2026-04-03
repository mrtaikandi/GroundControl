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
}