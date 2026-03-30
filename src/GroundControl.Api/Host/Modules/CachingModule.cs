using GroundControl.Api.Core.ChangeNotification;
using GroundControl.Api.Features.ClientApi;
using GroundControl.Api.Features.Clients;
using GroundControl.Host.Api;

namespace GroundControl.Api.Host.Modules;

[RunsAfter<ChangeNotificationModule>(Required = true)]
internal sealed class CachingModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<SnapshotCache>();
        builder.Services.AddHostedService<SnapshotCacheInvalidator>();

        if (builder.Configuration.GetValue<bool>("Cache:PrewarmOnStartup"))
        {
            builder.Services.AddHostedService<SnapshotCacheWarmupService>();
        }

        builder.Services.AddHostedService<ClientCleanupService>();
    }
}