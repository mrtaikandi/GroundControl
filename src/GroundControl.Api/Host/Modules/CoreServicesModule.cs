using Asp.Versioning;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Masking;
using GroundControl.Api.Shared.Resolvers;
using GroundControl.Host.Api;

namespace GroundControl.Api.Host.Modules;

internal sealed class CoreServicesModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IScopeResolver, ScopeResolver>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<AuditRecorder>();
        builder.Services.AddScoped<SensitiveValueMasker>();

        builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new HeaderApiVersionReader("api-version");
        });
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
    }
}