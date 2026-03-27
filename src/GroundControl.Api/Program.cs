using Asp.Versioning;
using GroundControl.Api.Features.Audit;
using GroundControl.Api.Features.ClientApi;
using GroundControl.Api.Features.Clients;
using GroundControl.Api.Features.ConfigEntries;
using GroundControl.Api.Features.Groups;
using GroundControl.Api.Features.PersonalAccessTokens;
using GroundControl.Api.Features.Projects;
using GroundControl.Api.Features.Roles;
using GroundControl.Api.Features.Scopes;
using GroundControl.Api.Features.Snapshots;
using GroundControl.Api.Features.Templates;
using GroundControl.Api.Features.Users;
using GroundControl.Api.Features.Variables;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Configuration;
using GroundControl.Api.Shared.Health;
using GroundControl.Api.Shared.Masking;
using GroundControl.Api.Shared.Notification;
using GroundControl.Api.Shared.Observability;
using GroundControl.Api.Shared.Resolvers;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Auth;
using GroundControl.Api.Shared.Security.Authorization;
using GroundControl.Api.Shared.Security.Certificate;
using GroundControl.Api.Shared.Security.KeyRing;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.MongoDb;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

builder.Services.AddValidation();
var appOptions = builder.Services.AddGroundControlOptions(builder.Configuration);

builder.Services.AddGroundControlMongo();

var dataProtectionBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("GroundControl");

var certProviderMode = builder.Configuration["DataProtection:CertificateProvider"];
if (certProviderMode is not null)
{
    RegisterCertificateProvider(certProviderMode, builder.Services);
    builder.Services.AddHostedService<CertificateStartupLogger>();
}

var dpMode = builder.Configuration["DataProtection:Mode"] ?? "FileSystem";
IKeyRingConfigurator keyRingConfigurator;

if (string.Equals(dpMode, "FileSystem", StringComparison.OrdinalIgnoreCase))
{
    keyRingConfigurator = new FileSystemKeyRingConfigurator();
}
else if (string.Equals(dpMode, "Certificate", StringComparison.OrdinalIgnoreCase))
{
    keyRingConfigurator = new CertificateKeyRingConfigurator(RequireCertificateProvider());
}
else if (string.Equals(dpMode, "Redis", StringComparison.OrdinalIgnoreCase))
{
    keyRingConfigurator = new RedisKeyRingConfigurator(RequireCertificateProvider());
}
else if (string.Equals(dpMode, "Azure", StringComparison.OrdinalIgnoreCase))
{
    keyRingConfigurator = new AzureKeyRingConfigurator();
}
else
{
    throw new InvalidOperationException(
        $"Unknown DataProtection:Mode '{dpMode}'. Supported values are 'FileSystem', 'Certificate', 'Redis', and 'Azure'.");
}

keyRingConfigurator.Configure(dataProtectionBuilder, builder.Configuration);
builder.Services.AddHostedService(sp =>
    new KeyRingStartupLogger(dpMode, sp.GetRequiredService<ILogger<KeyRingStartupLogger>>()));

builder.Services.AddSingleton<IValueProtector, DataProtectionValueProtector>();
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

IAuthConfigurator authConfigurator = appOptions.Security.AuthenticationMode switch
{
    AuthenticationMode.BuiltIn => new BuiltInAuthConfigurator(appOptions),
    AuthenticationMode.External => new ExternalAuthConfigurator(appOptions),
    _ => new NoAuthConfigurator()
};

authConfigurator.ConfigureServices(builder.Services, builder.Configuration);

var changeNotifierMode = builder.Configuration.GetValue<string>("ChangeNotifier:Mode");
if (string.Equals(changeNotifierMode, "MongoChangeStream", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<MongoChangeStreamNotifier>();
    builder.Services.AddSingleton<IChangeNotifier>(sp => sp.GetRequiredService<MongoChangeStreamNotifier>());
    builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<MongoChangeStreamNotifier>());
}
else
{
    builder.Services.AddSingleton<IChangeNotifier, InProcessChangeNotifier>();
}

builder.Services.AddSingleton<SnapshotCache>();
builder.Services.AddHostedService<SnapshotCacheInvalidator>();

if (builder.Configuration.GetValue<bool>("Cache:PrewarmOnStartup"))
{
    builder.Services.AddHostedService<SnapshotCacheWarmupService>();
}

builder.Services.AddHostedService<ClientCleanupService>();

builder.Services.AddAuditHandlers();
builder.Services.AddScopesHandlers();
builder.Services.AddGroupsHandlers();
builder.Services.AddRolesHandlers();
builder.Services.AddTemplatesHandlers();
builder.Services.AddProjectsHandlers();
builder.Services.AddConfigEntriesHandlers();
builder.Services.AddVariablesHandlers();
builder.Services.AddSnapshotsHandlers();
builder.Services.AddClientsHandlers();
builder.Services.AddClientApiHandlers();
builder.Services.AddPersonalAccessTokensHandlers();
builder.Services.AddUsersHandlers();

new AuthenticationBuilder(builder.Services)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { });

builder.Services
    .AddAuthorizationBuilder()
    .AddPermissionPolicies(Permissions.All);

builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddTransient<IClaimsTransformation, GroundControlClaimsTransformation>();

builder.Services.AddOpenApi();

var serviceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "GroundControl";
var otlpEndpoint = builder.Configuration["OpenTelemetry:Endpoint"];

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddMeter(GroundControlMetrics.MeterName);

        if (otlpEndpoint is not null)
        {
            metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    })
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddSource(GroundControlMetrics.ActivitySourceName);

        if (otlpEndpoint is not null)
        {
            tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    });

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeScopes = true;

    if (otlpEndpoint is not null)
    {
        logging.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
    }
});

builder.Services.AddHealthChecks()
    .AddMongoDb(
        dbFactory: sp => sp.GetRequiredService<IMongoDbContext>().Database,
        name: "mongodb",
        tags: ["ready"],
        timeout: TimeSpan.FromSeconds(5))
    .AddCheck<ChangeNotifierHealthCheck>("change-notifier", tags: ["ready"]);

var app = builder.Build();

authConfigurator = app.Services.GetRequiredService<IAuthConfigurator>();
authConfigurator.ConfigureMiddleware(app);
authConfigurator.MapEndpoints(app);

app.MapAuditEndpoints();
app.MapScopesEndpoints();
app.MapGroupsEndpoints();
app.MapRolesEndpoints();
app.MapTemplatesEndpoints();
app.MapProjectsEndpoints();
app.MapConfigEntriesEndpoints();
app.MapVariablesEndpoints();
app.MapSnapshotsEndpoints();
app.MapClientsEndpoints();
app.MapClientApiEndpoints();
app.MapPersonalAccessTokensEndpoints();
app.MapUsersEndpoints();

app.MapOpenApi();
app.MapHealthChecks("/healthz/liveness", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    Predicate = p => p.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});


app.Run();

void RegisterCertificateProvider(string mode, IServiceCollection services)
{
    if (string.Equals(mode, "FileSystem", StringComparison.OrdinalIgnoreCase))
    {
        services.AddSingleton<IDataProtectionCertificateProvider, FileSystemCertificateProvider>();
    }
    else if (string.Equals(mode, "AzureBlob", StringComparison.OrdinalIgnoreCase))
    {
        services.AddSingleton<IDataProtectionCertificateProvider, AzureBlobCertificateProvider>();
    }
    else
    {
        throw new InvalidOperationException(
            $"Unknown DataProtection:CertificateProvider mode: '{mode}'. Supported values are 'FileSystem' and 'AzureBlob'.");
    }
}

// Constructs a certificate provider for key ring configuration at startup.
// A separate DI-registered instance handles runtime use with proper logging.
IDataProtectionCertificateProvider RequireCertificateProvider()
{
    if (certProviderMode is null)
    {
        throw new InvalidOperationException(
            $"DataProtection:CertificateProvider must be configured when using '{dpMode}' key ring mode.");
    }

    if (string.Equals(certProviderMode, "FileSystem", StringComparison.OrdinalIgnoreCase))
    {
        return new FileSystemCertificateProvider(
            builder.Configuration,
            NullLoggerFactory.Instance.CreateLogger<FileSystemCertificateProvider>());
    }

    if (string.Equals(certProviderMode, "AzureBlob", StringComparison.OrdinalIgnoreCase))
    {
        return new AzureBlobCertificateProvider(
            builder.Configuration,
            NullLoggerFactory.Instance.CreateLogger<AzureBlobCertificateProvider>());
    }

    // Unreachable: RegisterCertificateProvider already validated the mode.
    throw new InvalidOperationException(
        $"Unknown DataProtection:CertificateProvider mode: '{certProviderMode}'.");
}