using Microsoft.AspNetCore.Authentication;

namespace GroundControl.Api.Shared.Security.Authentication;

/// <summary>
/// Defines the contract for configuring authentication and authorization.
/// </summary>
internal interface IAuthenticationBuilder
{
    /// <summary>
    /// Configures authentication and authorization services.
    /// </summary>
    AuthenticationBuilder Build(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    /// Configures authentication and authorization middleware.
    /// </summary>
    void Configure(WebApplication app);
}