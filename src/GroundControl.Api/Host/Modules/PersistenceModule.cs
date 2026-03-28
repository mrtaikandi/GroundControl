using GroundControl.Host.Api;
using GroundControl.Persistence.MongoDb;

namespace GroundControl.Api.Host.Modules;

[RunsAfter<ConfigurationModule>(Required = true)]
internal sealed class PersistenceModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddGroundControlMongo();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
    }
}