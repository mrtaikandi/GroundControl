using GroundControl.Api.Core.ChangeNotification;
using GroundControl.Host.Api;
using GroundControl.Persistence.MongoDb;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace GroundControl.Api.Core.HealthChecks;

[RunsAfter<ApplicationModule>(Required = true)]
[RunsAfter<ChangeNotificationModule>(Required = true)]
internal sealed class HealthChecksModule : IWebApiModule
{
    internal const string HealthEndpointPrefix = "/healthz";

    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddMongoDb(
                dbFactory: sp => sp.GetRequiredService<IMongoDbContext>().Database,
                name: "mongodb",
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(5))
            .AddCheck<ChangeNotifierHealthCheck>("change-notifier", tags: ["ready"]);
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        app.MapHealthChecks($"{HealthEndpointPrefix}/liveness", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks($"{HealthEndpointPrefix}/ready", new HealthCheckOptions
        {
            Predicate = p => p.Tags.Contains("ready"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });
    }
}