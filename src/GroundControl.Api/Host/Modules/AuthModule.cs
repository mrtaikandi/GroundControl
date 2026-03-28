using GroundControl.Api.Shared.Configuration;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Auth;
using GroundControl.Api.Shared.Security.Authorization;
using GroundControl.Host.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace GroundControl.Api.Host.Modules;

[RunsAfter<CoreServicesModule>]
[RunsAfter<ConfigurationModule>(Required = true)]
internal sealed class AuthModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetRequiredSection(GroundControlOptions.SectionName);
        var appOptions = section.Get<GroundControlOptions>()!;

        IAuthConfigurator authConfigurator = appOptions.Security.AuthenticationMode switch
        {
            AuthenticationMode.BuiltIn => new BuiltInAuthConfigurator(appOptions),
            AuthenticationMode.External => new ExternalAuthConfigurator(appOptions),
            _ => new NoAuthConfigurator()
        };

        authConfigurator.ConfigureServices(builder.Services, builder.Configuration);

        new AuthenticationBuilder(builder.Services)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { });

        builder.Services
            .AddAuthorizationBuilder()
            .AddPermissionPolicies(Permissions.All);

        builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        builder.Services.AddTransient<IClaimsTransformation, GroundControlClaimsTransformation>();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        var authConfigurator = app.Services.GetRequiredService<IAuthConfigurator>();
        authConfigurator.ConfigureMiddleware(app);
        authConfigurator.MapEndpoints(app);
    }
}