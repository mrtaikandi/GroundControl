using GroundControl.Api.Features.Users.Contracts;
using GroundControl.Api.Shared.Validation;

namespace GroundControl.Api.Features.Users;

internal static class UsersEndpoints
{
    public static IServiceCollection AddUsersHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<CreateUserHandler>();
        services.AddTransient<GetUserHandler>();
        services.AddTransient<ListUsersHandler>();
        services.AddTransient<UpdateUserHandler>();
        services.AddTransient<DeleteUserHandler>();
        services.AddTransient<ChangePasswordHandler>();

        services.AddTransient<IAsyncValidator<CreateUserRequest>, CreateUserValidator>();

        return services;
    }

    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/users")
            .WithTags("Users");

        CreateUserHandler.Endpoint(group);
        GetUserHandler.Endpoint(group);
        ListUsersHandler.Endpoint(group);
        UpdateUserHandler.Endpoint(group);
        DeleteUserHandler.Endpoint(group);
        ChangePasswordHandler.Endpoint(group);

        return endpoints;
    }
}