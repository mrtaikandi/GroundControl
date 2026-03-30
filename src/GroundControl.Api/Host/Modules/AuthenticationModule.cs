using System.Diagnostics;
using GroundControl.Api.Shared.Extensions.Options;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Authentication;
using GroundControl.Api.Shared.Security.Authorization;
using GroundControl.Host.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using AuthenticationOptions = GroundControl.Api.Shared.Security.Authentication.AuthenticationOptions;

namespace GroundControl.Api.Host.Modules;

[RunsAfter<AppCommonModule>]
internal sealed class AuthenticationModule(AuthenticationOptions options) : IWebApiModule<AuthenticationOptions>
{
    private IAuthenticationBuilder? _authenticationBuilder;

    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<AuthenticationOptions, AuthenticationOptions.Validator>(options);

        _authenticationBuilder = options.Mode switch
        {
            AuthenticationMode.BuiltIn => new BuiltInAuthenticationBuilder(options),
            AuthenticationMode.External => new ExternalAuthenticationBuilder(options),
            _ => new NoAuthenticationBuilder()
        };

        _authenticationBuilder.Build(builder.Services, builder.Configuration)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { });

        builder.Services
            .AddAuthorizationBuilder()
            .AddPermissionPolicies(Permissions.All);

        builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        builder.Services.AddTransient<IClaimsTransformation, GroundControlClaimsTransformation>();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        Debug.Assert(_authenticationBuilder != null, $"{nameof(_authenticationBuilder)} != null");
        _authenticationBuilder.Configure(app);
    }
}