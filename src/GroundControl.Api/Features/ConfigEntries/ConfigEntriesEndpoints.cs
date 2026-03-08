namespace GroundControl.Api.Features.ConfigEntries;

internal static class ConfigEntriesEndpoints
{
    public static IServiceCollection AddConfigEntriesHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<CreateConfigEntryHandler>();
        services.AddTransient<GetConfigEntryHandler>();
        services.AddTransient<ListConfigEntriesHandler>();
        services.AddTransient<UpdateConfigEntryHandler>();
        services.AddTransient<DeleteConfigEntryHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapConfigEntriesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/config-entries")
            .WithTags("ConfigEntries");

        CreateConfigEntryHandler.Endpoint(group);
        GetConfigEntryHandler.Endpoint(group);
        ListConfigEntriesHandler.Endpoint(group);
        UpdateConfigEntryHandler.Endpoint(group);
        DeleteConfigEntryHandler.Endpoint(group);

        return endpoints;
    }
}