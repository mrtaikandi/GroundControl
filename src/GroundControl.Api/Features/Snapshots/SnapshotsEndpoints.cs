namespace GroundControl.Api.Features.Snapshots;

internal static class SnapshotsEndpoints
{
    public static IServiceCollection AddSnapshotsHandlers(this IServiceCollection services)
    {
        services.AddTransient<VariableInterpolator>();
        services.AddTransient<SnapshotPublisher>();

        return services;
    }

    public static IEndpointRouteBuilder MapSnapshotsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        return endpoints;
    }
}