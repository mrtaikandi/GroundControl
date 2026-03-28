using GroundControl.Api.Shared.Configuration;
using GroundControl.Host.Api;

namespace GroundControl.Api.Host.Modules;

internal sealed class ConfigurationModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

        builder.Services.AddValidation();
        builder.Services.AddGroundControlOptions(builder.Configuration);
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
    }
}