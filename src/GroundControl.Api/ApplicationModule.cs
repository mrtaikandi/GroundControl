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
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddValidation();
        builder.Services.AddHttpContextAccessor();

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
}