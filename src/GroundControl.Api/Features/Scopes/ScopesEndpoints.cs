using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Api.Shared.Validation;

namespace GroundControl.Api.Features.Scopes;

internal static class ScopesEndpoints
{
    public static IServiceCollection AddScopesHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<CreateScopeHandler>();
        services.AddTransient<GetScopeHandler>();
        services.AddTransient<ListScopesHandler>();
        services.AddTransient<UpdateScopeHandler>();
        services.AddTransient<DeleteScopeHandler>();

        services.AddTransient<IAsyncValidator<CreateScopeRequest>, CreateScopeValidator>();

        return services;
    }

    public static IEndpointRouteBuilder MapScopesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/scopes")
            .WithTags("Scopes");

        CreateScopeHandler.Endpoint(group);
        GetScopeHandler.Endpoint(group);
        ListScopesHandler.Endpoint(group);
        UpdateScopeHandler.Endpoint(group);
        DeleteScopeHandler.Endpoint(group);

        return endpoints;
    }
}