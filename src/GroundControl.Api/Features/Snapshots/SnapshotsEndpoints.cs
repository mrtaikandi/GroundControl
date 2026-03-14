namespace GroundControl.Api.Features.Snapshots;

internal static class SnapshotsEndpoints
{
    public static IServiceCollection AddSnapshotsHandlers(this IServiceCollection services)
    {
        services.AddTransient<VariableInterpolator>();

        return services;
    }

    public static IEndpointRouteBuilder MapSnapshotsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        return endpoints;
    }
}