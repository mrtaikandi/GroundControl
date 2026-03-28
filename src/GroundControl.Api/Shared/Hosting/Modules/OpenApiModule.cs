using GroundControl.Host.Api;

namespace GroundControl.Api.Shared.Hosting.Modules;

internal sealed class OpenApiModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        app.MapOpenApi();
    }
}