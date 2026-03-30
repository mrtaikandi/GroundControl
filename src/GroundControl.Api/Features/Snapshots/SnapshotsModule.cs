using GroundControl.Api.Core.Authentication;
using GroundControl.Host.Api;

namespace GroundControl.Api.Features.Snapshots;

[RunsAfter<ApplicationModule>(Required = true)]
[RunsAfter<AuthenticationModule>(Required = true)]
internal sealed class SnapshotsModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddTransient<VariableInterpolator>();
        builder.Services.AddTransient<SnapshotPublisher>();
        builder.Services.AddTransient<PublishSnapshotHandler>();
        builder.Services.AddTransient<ActivateSnapshotHandler>();
        builder.Services.AddTransient<GetSnapshotHandler>();
        builder.Services.AddTransient<ListSnapshotsHandler>();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        var group = app.MapGroup("/api/projects/{projectId:guid}/snapshots")
            .WithTags("Snapshots");

        PublishSnapshotHandler.Endpoint(group);
        ActivateSnapshotHandler.Endpoint(group);
        GetSnapshotHandler.Endpoint(group);
        ListSnapshotsHandler.Endpoint(group);
    }
}