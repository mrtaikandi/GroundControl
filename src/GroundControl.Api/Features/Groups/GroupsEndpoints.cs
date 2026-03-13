using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Api.Shared.Validation;

namespace GroundControl.Api.Features.Groups;

internal static class GroupsEndpoints
{
    public static IServiceCollection AddGroupsHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<CreateGroupHandler>();
        services.AddTransient<GetGroupHandler>();
        services.AddTransient<ListGroupsHandler>();
        services.AddTransient<UpdateGroupHandler>();
        services.AddTransient<DeleteGroupHandler>();

        services.AddTransient<IAsyncValidator<CreateGroupRequest>, CreateGroupValidator>();

        return services;
    }

    public static IEndpointRouteBuilder MapGroupsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/groups")
            .WithTags("Groups");

        CreateGroupHandler.Endpoint(group);
        GetGroupHandler.Endpoint(group);
        ListGroupsHandler.Endpoint(group);
        UpdateGroupHandler.Endpoint(group);
        DeleteGroupHandler.Endpoint(group);

        return endpoints;
    }
}