using AspNetCore.Identity.MongoDbCore.Models;
using GroundControl.Api.Features.Auth;
using GroundControl.Api.Shared.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace GroundControl.Api.Shared.Security.Auth;

internal sealed class BuiltInAuthConfigurator : IAuthConfigurator
{
    private const string AuthenticateScheme = "smart";
    private readonly GroundControlOptions _options;

    public BuiltInAuthConfigurator(GroundControlOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var builtIn = _options.Security.BuiltIn;

        ValidateJwtSecret(builtIn.Jwt);

        var connectionString = configuration.GetConnectionString("Storage")
                               ?? throw new InvalidOperationException("ConnectionStrings:Storage is not configured.");

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
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, PatBearerHandler>(PatBearerHandler.SchemeName, _ => { })
            .AddPolicyScheme(AuthenticateScheme, "Cookie, JWT, or PAT Bearer", options =>
            {
                options.ForwardDefaultSelector = ctx =>
                {
                    var authorization = ctx.Request.Headers.Authorization.ToString();
                    if (string.IsNullOrEmpty(authorization))
                    {
                        return CookieAuthenticationDefaults.AuthenticationScheme;
                    }

                    // Route gc_pat_ tokens to the PAT handler
                    if (authorization.StartsWith("Bearer gc_pat_", StringComparison.OrdinalIgnoreCase))
                    {
                        return PatBearerHandler.SchemeName;
                    }

                    return JwtBearerDefaults.AuthenticationScheme;
                };
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.Name = builtIn.Cookie.Name;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.SlidingExpiration = builtIn.Cookie.SlidingExpiration;
                options.ExpireTimeSpan = builtIn.Cookie.ExpireTimeSpan;

                // Suppress redirects — return 401 for APIs
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

        services.AddAuthHandlers();
        services.AddSingleton<IAuthConfigurator>(this);
        services.AddHostedService<AdminSeedService>();
    }

    public void ConfigureMiddleware(IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAuthEndpoints();
    }

    private static void ValidateJwtSecret(JwtOptions jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt.Secret))
        {
            throw new InvalidOperationException("JWT signing key is not configured. Set 'GroundControl__Security__BuiltIn__Jwt__Secret' environment variable.");
        }

        try
        {
            var keyBytes = Convert.FromBase64String(jwt.Secret);
            if (keyBytes.Length < 32)
            {
                throw new InvalidOperationException("JWT signing key must be at least 256 bits (32 bytes). The configured key is too short.");
            }
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "JWT signing key must be a valid Base64-encoded string. Check the 'GroundControl__Security__BuiltIn__Jwt__Secret' environment variable.");
        }
    }
}