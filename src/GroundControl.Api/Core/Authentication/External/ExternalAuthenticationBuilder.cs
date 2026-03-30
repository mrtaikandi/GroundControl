using System.Security.Claims;
using GroundControl.Api.Core.Authentication.BuiltIn;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace GroundControl.Api.Core.Authentication.External;

internal sealed class ExternalAuthenticationBuilder : IAuthenticationBuilder
{
    private const string AuthenticateScheme = "smart-external";
    private readonly AuthenticationOptions _options;

    public ExternalAuthenticationBuilder(AuthenticationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public AuthenticationBuilder Build(IServiceCollection services, IConfiguration configuration)
    {
        var external = _options.External;

        services.AddSingleton(external);
        services.AddSingleton<JitProvisioningService>();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = AuthenticateScheme;
                options.DefaultAuthenticateScheme = AuthenticateScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, PatBearerHandler>(PatBearerHandler.SchemeName, _ => { })
            .AddPolicyScheme(AuthenticateScheme, "Cookie, OIDC Bearer, or PAT", options =>
            {
                options.ForwardDefaultSelector = ctx =>
                {
                    var authorization = ctx.Request.Headers.Authorization.ToString();
                    if (string.IsNullOrEmpty(authorization))
                    {
                        return CookieAuthenticationDefaults.AuthenticationScheme;
                    }

                    if (authorization.StartsWith("Bearer gc_pat_", StringComparison.OrdinalIgnoreCase))
                    {
                        return PatBearerHandler.SchemeName;
                    }

                    return JwtBearerDefaults.AuthenticationScheme;
                };
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.Name = external.Cookie.Name;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.SlidingExpiration = external.Cookie.SlidingExpiration;
                options.ExpireTimeSpan = external.Cookie.ExpireTimeSpan;

                options.Events.OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            })
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.Authority = external.Authority;
                options.ClientId = external.ClientId;
                options.ClientSecret = external.ClientSecret;
                options.ResponseType = external.ResponseType;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.CallbackPath = external.CallbackPath;

                options.Scope.Clear();
                foreach (var scope in external.Scopes)
                {
                    options.Scope.Add(scope);
                }

                options.Events.OnTicketReceived = OnTicketReceivedAsync;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = external.Authority;
                options.Audience = external.Audience ?? external.ClientId;
            });

        return new AuthenticationBuilder(services);
    }

    public void Configure(WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    private static async Task OnTicketReceivedAsync(TicketReceivedContext ctx)
    {
        var jitService = ctx.HttpContext.RequestServices.GetRequiredService<JitProvisioningService>();

        var result = await jitService.ProvisionAsync(ctx.Principal!, ctx.HttpContext.RequestAborted);
        if (!result.Succeeded)
        {
            ctx.Fail(result.Error!);
            return;
        }

        // Add the domain user ID to the principal for downstream claims transformation
        ctx.Principal!.AddIdentity(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, result.User!.Id.ToString())
        ]));
    }

}