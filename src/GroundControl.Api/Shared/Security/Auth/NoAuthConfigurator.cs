using Microsoft.AspNetCore.Authentication;

namespace GroundControl.Api.Shared.Security.Auth;

internal sealed class NoAuthConfigurator : IAuthConfigurator
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = NoAuthHandler.SchemeName;
            options.DefaultChallengeScheme = NoAuthHandler.SchemeName;
        })
        .AddScheme<AuthenticationSchemeOptions, NoAuthHandler>(NoAuthHandler.SchemeName, _ => { });

        services.AddSingleton<IAuthConfigurator>(this);
    }

    public void ConfigureMiddleware(IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // No endpoints needed for NoAuth mode
    }
}