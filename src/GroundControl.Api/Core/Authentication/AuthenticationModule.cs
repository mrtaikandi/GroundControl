using System.Diagnostics;
using GroundControl.Api.Core.Authentication.BuiltIn;
using GroundControl.Api.Core.Authentication.External;
using GroundControl.Api.Core.Authentication.NoAuth;
using GroundControl.Api.Shared.Extensions.Options;
using GroundControl.Api.Shared.Security;
using GroundControl.Host.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace GroundControl.Api.Core.Authentication;


[RunsAfter<AppCommonModule>]
internal sealed class AuthenticationModule(AuthenticationOptions options) : IWebApiModule<AuthenticationOptions>
{
    private IAuthenticationBuilder? _authenticationBuilder;

    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        _authenticationBuilder = AddAuthenticationServices(builder, options);

        if (options.Mode != AuthenticationMode.External)
        {
            AddAuthHandlers(builder.Services);
        }
        else
        {
            AddExternalAuthHandlers(builder.Services);
        }
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        Debug.Assert(_authenticationBuilder != null, $"{nameof(_authenticationBuilder)} != null");
        _authenticationBuilder.Configure(app);

        var group = app.MapGroup("/auth")
            .WithTags("Auth");

        if (options.Mode != AuthenticationMode.External)
        {
            MapAuthEndpoints(group);
        }
        else
        {
            MapExternalAuthEndpoints(group);
        }
    }

    private static IAuthenticationBuilder AddAuthenticationServices(WebApplicationBuilder builder, AuthenticationOptions options)
    {
        builder.Services.AddOptions<AuthenticationOptions, AuthenticationOptions.Validator>(options);

        IAuthenticationBuilder authenticationBuilder = options.Mode switch
        {
            AuthenticationMode.BuiltIn => new BuiltInAuthenticationBuilder(options),
            AuthenticationMode.External => new ExternalAuthenticationBuilder(options),
            _ => new NoAuthenticationBuilder()
        };

        authenticationBuilder.Build(builder.Services, builder.Configuration)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { });

        builder.Services
            .AddAuthorizationBuilder()
            .AddPermissionPolicies(Permissions.All);

        builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        builder.Services.AddTransient<IClaimsTransformation, GroundControlClaimsTransformation>();

        return authenticationBuilder;
    }

    private static void AddAuthHandlers(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<LoginHandler>();
        services.AddTransient<LogoutHandler>();
        services.AddTransient<TokenLoginHandler>();
        services.AddTransient<TokenRefreshHandler>();
        services.AddTransient<GetCurrentUserHandler>();
    }

    private static void MapAuthEndpoints(IEndpointRouteBuilder group)
    {
        LoginHandler.Endpoint(group);
        LogoutHandler.Endpoint(group);
        TokenLoginHandler.Endpoint(group);
        TokenRefreshHandler.Endpoint(group);
        GetCurrentUserHandler.Endpoint(group);
    }

    private static void AddExternalAuthHandlers(IServiceCollection services)
    {
        services.AddTransient<GetCurrentUserHandler>();
    }

    private static void MapExternalAuthEndpoints(IEndpointRouteBuilder group)
    {
        ExternalLoginHandler.Endpoint(group);
        ExternalCallbackHandler.Endpoint(group);
        GetCurrentUserHandler.Endpoint(group);
    }
}