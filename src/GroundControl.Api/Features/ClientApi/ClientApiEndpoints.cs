namespace GroundControl.Api.Features.ClientApi;

internal static class ClientApiEndpoints
{
    public static IServiceCollection AddClientApiHandlers(this IServiceCollection services)
    {
        services.AddTransient<GetConfigHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapClientApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/client")
            .WithTags("ClientApi");

        GetConfigHandler.Endpoint(group);
        ClientHealthHandler.Endpoint(group);

        return endpoints;
    }
}