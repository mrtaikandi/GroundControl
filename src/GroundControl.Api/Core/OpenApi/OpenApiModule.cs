using GroundControl.Host.Api;
using Scalar.AspNetCore;

namespace GroundControl.Api.Core.OpenApi;

internal sealed class OpenApiModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        app.MapOpenApi();

        if (app.Environment.IsDevelopment())
        {
            app.MapScalarApiReference(options =>
            {
                options.DarkMode = true;
                options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
            });
        }
    }
}