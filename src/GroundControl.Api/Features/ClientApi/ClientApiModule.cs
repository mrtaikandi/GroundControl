using GroundControl.Api.Core.Authentication;
using GroundControl.Api.Core.ChangeNotification;
using GroundControl.Api.Features.Clients;
using GroundControl.Host.Api;

namespace GroundControl.Api.Features.ClientApi;

[RunsAfter<ApplicationModule>(Required = true)]
[RunsAfter<AuthenticationModule>(Required = true)]
[RunsAfter<ChangeNotificationModule>(Required = true)]
internal sealed class ClientApiModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddTransient<GetConfigHandler>();
        builder.Services.AddTransient<StreamConfigHandler>();

        builder.Services.AddSingleton<SnapshotCache>();
        builder.Services.AddHostedService<SnapshotCacheInvalidator>();

        if (builder.Configuration.GetValue<bool>("Cache:PrewarmOnStartup"))
        {
            builder.Services.AddHostedService<SnapshotCacheWarmupService>();
        }

        builder.Services.AddHostedService<ClientCleanupService>();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        var group = app.MapGroup("/client")
            .WithTags("ClientApi");

        GetConfigHandler.Endpoint(group);
        StreamConfigHandler.Endpoint(group);
        ClientHealthHandler.Endpoint(group);
    }
}