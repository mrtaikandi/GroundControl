using GroundControl.Api.Features.Clients.Contracts;
using GroundControl.Api.Shared.Validation;

namespace GroundControl.Api.Features.Clients;

internal static class ClientsEndpoints
{
    public static IServiceCollection AddClientsHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<CreateClientHandler>();
        services.AddTransient<GetClientHandler>();
        services.AddTransient<ListClientsHandler>();
        services.AddTransient<UpdateClientHandler>();
        services.AddTransient<DeleteClientHandler>();

        services.AddTransient<IAsyncValidator<CreateClientRequest>, CreateClientValidator>();
        services.AddTransient<DeleteClientValidator>();

        return services;
    }

    public static IEndpointRouteBuilder MapClientsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/projects/{projectId:guid}/clients")
            .WithTags("Clients");

        CreateClientHandler.Endpoint(group);
        GetClientHandler.Endpoint(group);
        ListClientsHandler.Endpoint(group);
        UpdateClientHandler.Endpoint(group);
        DeleteClientHandler.Endpoint(group);

        return endpoints;
    }
}