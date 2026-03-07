namespace GroundControl.Api.Shared.Security.Auth;

/// <summary>
/// Defines the contract for configuring authentication and authorization.
/// </summary>
internal interface IAuthConfigurator
{
    /// <summary>
    /// Configures authentication and authorization services.
    /// </summary>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    /// Configures authentication and authorization middleware.
    /// </summary>
    void ConfigureMiddleware(IApplicationBuilder app);

    /// <summary>
    /// Maps authentication-related endpoints (e.g., login, callback).
    /// </summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}