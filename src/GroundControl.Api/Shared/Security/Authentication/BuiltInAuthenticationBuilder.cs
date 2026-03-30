using AspNetCore.Identity.MongoDbCore.Models;
using GroundControl.Api.Features.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace GroundControl.Api.Shared.Security.Authentication;

internal sealed class BuiltInAuthenticationBuilder : IAuthenticationBuilder
{
    private const string AuthenticateScheme = "smart";
    private readonly AuthenticationOptions _authOptions;

    public BuiltInAuthenticationBuilder(AuthenticationOptions authOptions)
    {
        _authOptions = authOptions ?? throw new ArgumentNullException(nameof(authOptions));
    }

    public AuthenticationBuilder Build(IServiceCollection services, IConfiguration configuration)
    {
        var builtIn = _authOptions.BuiltIn;

        var connectionString = configuration.GetConnectionString("Storage") ?? throw new InvalidOperationException("ConnectionStrings:Storage is not configured.");

        var databaseName = configuration.GetValue<string>("Persistence:MongoDb:DatabaseName") ?? "GroundControl";

        services.AddIdentity<MongoIdentityUser<Guid>, MongoIdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = builtIn.Password.RequiredLength;
                options.Password.RequireDigit = builtIn.Password.RequireDigit;
                options.Password.RequireUppercase = builtIn.Password.RequireUppercase;
                options.Password.RequireLowercase = builtIn.Password.RequireLowercase;
                options.Password.RequireNonAlphanumeric = builtIn.Password.RequireNonAlphanumeric;
                options.Lockout.MaxFailedAccessAttempts = builtIn.Lockout.MaxFailedAttempts;
                options.Lockout.DefaultLockoutTimeSpan = builtIn.Lockout.LockoutDuration;
            })
            .AddMongoDbStores<MongoIdentityUser<Guid>, MongoIdentityRole<Guid>, Guid>(connectionString, databaseName)
            .AddDefaultTokenProviders();

        // Override Identity's default scheme settings — AddIdentity sets these to its own cookie scheme
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = AuthenticateScheme;
                options.DefaultAuthenticateScheme = AuthenticateScheme;
                options.DefaultChallengeScheme = AuthenticateScheme;
                options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, PatBearerHandler>(PatBearerHandler.SchemeName, _ => { })
            .AddPolicyScheme(AuthenticateScheme, "Cookie, JWT, or PAT Bearer", options =>
            {
                options.ForwardDefaultSelector = ctx =>
                {
                    var authorization = ctx.Request.Headers.Authorization.ToString();
                    if (string.IsNullOrEmpty(authorization))
                    {
                        // SignInManager signs in via Identity.Application — route cookie auth there
                        return IdentityConstants.ApplicationScheme;
                    }

                    // Route gc_pat_ tokens to the PAT handler
                    if (authorization.StartsWith("Bearer gc_pat_", StringComparison.OrdinalIgnoreCase))
                    {
                        return PatBearerHandler.SchemeName;
                    }

                    return JwtBearerDefaults.AuthenticationScheme;
                };
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Convert.FromBase64String(builtIn.Jwt.Secret)),
                    ValidateIssuer = true,
                    ValidIssuer = builtIn.Jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = builtIn.Jwt.Audience,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        // Customize the Identity.Application cookie scheme (registered by AddIdentity)
        services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.Name = builtIn.Cookie.Name;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.SlidingExpiration = builtIn.Cookie.SlidingExpiration;
            options.ExpireTimeSpan = builtIn.Cookie.ExpireTimeSpan;

            // Suppress redirects — return 401/403 for APIs
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
        });

        var csrfOptions = _authOptions.Csrf;
        services.AddSingleton(csrfOptions);
        services.AddAntiforgery(options =>
        {
            options.HeaderName = csrfOptions.HeaderName;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });

        services.AddAuthHandlers();
        services.AddHostedService<AdminSeedService>();

        return new AuthenticationBuilder(services);
    }

    public void Configure(WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<CsrfProtectionMiddleware>();

        app.MapAuthEndpoints();
    }
}