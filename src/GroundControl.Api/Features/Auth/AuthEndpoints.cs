namespace GroundControl.Api.Features.Auth;

internal static class AuthEndpoints
{
    public static IServiceCollection AddAuthHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<LoginHandler>();
        services.AddTransient<LogoutHandler>();
        services.AddTransient<TokenLoginHandler>();
        services.AddTransient<TokenRefreshHandler>();
        services.AddTransient<GetCurrentUserHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/auth")
            .WithTags("Auth");

        LoginHandler.Endpoint(group);
        LogoutHandler.Endpoint(group);
        TokenLoginHandler.Endpoint(group);
        TokenRefreshHandler.Endpoint(group);
        GetCurrentUserHandler.Endpoint(group);

        return endpoints;
    }

    public static IServiceCollection AddExternalAuthHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<GetCurrentUserHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapExternalAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/auth")
            .WithTags("Auth");

        ExternalLoginHandler.Endpoint(group);
        ExternalCallbackHandler.Endpoint(group);
        GetCurrentUserHandler.Endpoint(group);

        return endpoints;
    }
}