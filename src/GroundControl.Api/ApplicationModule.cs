using System.Net;
using Asp.Versioning;
using GroundControl.Api.Features.Roles;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Resolvers;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Host.Api;
using GroundControl.Persistence.MongoDb;

namespace GroundControl.Api;

internal sealed class ApplicationModule : IWebApiModule
{
    private const string LocalDevelopmentCorsPolicyName = "LocalDevelopment";

    public void OnApplicationConfiguration(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseCors(LocalDevelopmentCorsPolicyName);
        }
    }

    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddValidation();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(LocalDevelopmentCorsPolicyName, policy => policy
                .SetIsOriginAllowed(IsLocalDevelopmentOrigin)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
        });

        builder.Services.AddGroundControlMongo();

        builder.Services.AddSingleton<IScopeResolver, ScopeResolver>();
        builder.Services.AddScoped<AuditRecorder>();
        builder.Services.AddSingleton<SensitiveSourceValueProtector>();
        builder.Services.AddScoped<SensitiveValueMasker>();

        builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new HeaderApiVersionReader("api-version");
        });

        builder.Services.AddHostedService<RoleSeedService>();
    }

    private static bool IsLocalDevelopmentOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address);
    }
}