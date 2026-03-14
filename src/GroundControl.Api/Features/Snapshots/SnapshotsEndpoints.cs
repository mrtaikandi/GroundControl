namespace GroundControl.Api.Features.Snapshots;

internal static class SnapshotsEndpoints
{
    public static IServiceCollection AddSnapshotsHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<VariableInterpolator>();
        services.AddTransient<SnapshotPublisher>();
        services.AddTransient<PublishSnapshotHandler>();
        services.AddTransient<ActivateSnapshotHandler>();
        services.AddTransient<GetSnapshotHandler>();
        services.AddTransient<ListSnapshotsHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapSnapshotsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/projects/{projectId:guid}/snapshots")
            .WithTags("Snapshots");

        PublishSnapshotHandler.Endpoint(group);
        ActivateSnapshotHandler.Endpoint(group);
        GetSnapshotHandler.Endpoint(group);
        ListSnapshotsHandler.Endpoint(group);

        return endpoints;
    }
}