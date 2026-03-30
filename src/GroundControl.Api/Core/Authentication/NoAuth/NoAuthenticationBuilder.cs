using Microsoft.AspNetCore.Authentication;

namespace GroundControl.Api.Core.Authentication.NoAuth;

internal sealed class NoAuthenticationBuilder : IAuthenticationBuilder
{
    public AuthenticationBuilder Build(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = NoAuthHandler.SchemeName;
                options.DefaultChallengeScheme = NoAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, NoAuthHandler>(NoAuthHandler.SchemeName, _ => { });

        return new AuthenticationBuilder(services);
    }

    public void Configure(WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }
}