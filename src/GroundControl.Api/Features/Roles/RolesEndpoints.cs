using GroundControl.Api.Features.Roles.Contracts;
using GroundControl.Api.Shared.Validation;

namespace GroundControl.Api.Features.Roles;

internal static class RolesEndpoints
{
    public static IServiceCollection AddRolesHandlers(this IServiceCollection services)
    {
        services.AddTransient<CreateRoleHandler>();
        services.AddTransient<GetRoleHandler>();
        services.AddTransient<ListRolesHandler>();
        services.AddTransient<UpdateRoleHandler>();
        services.AddTransient<DeleteRoleHandler>();

        services.AddTransient<IAsyncValidator<CreateRoleRequest>, CreateRoleValidator>();
        services.AddTransient<IAsyncValidator<UpdateRoleRequest>, UpdateRoleValidator>();
        services.AddTransient<DeleteRoleValidator>();

        return services;
    }

    public static IEndpointRouteBuilder MapRolesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/roles")
            .WithTags("Roles");

        CreateRoleHandler.Endpoint(group);
        GetRoleHandler.Endpoint(group);
        ListRolesHandler.Endpoint(group);
        UpdateRoleHandler.Endpoint(group);
        DeleteRoleHandler.Endpoint(group);

        return endpoints;
    }
}