using GroundControl.Api.Shared.Activity;
using GroundControl.Host.Api;

namespace GroundControl.Api.Features.Activity;

internal sealed class ActivityModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ILiveActivityTracker, LiveActivityTracker>();
        builder.Services.AddTransient<GetActivitySummaryHandler>();
        builder.Services.AddTransient<StreamActivityHandler>();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        var group = app.MapGroup("/activity")
            .WithTags("Activity")
            .RequireAuthorization();

        GetActivitySummaryHandler.Endpoint(group);
        StreamActivityHandler.Endpoint(group);
    }
}