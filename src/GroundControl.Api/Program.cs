using Asp.Versioning;
using GroundControl.Api.Features.ClientApi;
using GroundControl.Api.Features.Clients;
using GroundControl.Api.Features.ConfigEntries;
using GroundControl.Api.Features.Groups;
using GroundControl.Api.Features.Projects;
using GroundControl.Api.Features.Roles;
using GroundControl.Api.Features.Scopes;
using GroundControl.Api.Features.Snapshots;
using GroundControl.Api.Features.Templates;
using GroundControl.Api.Features.Variables;
using GroundControl.Api.Shared.Configuration;
using GroundControl.Api.Shared.Health;
using GroundControl.Api.Shared.Notification;
using GroundControl.Api.Shared.Resolvers;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Auth;
using GroundControl.Api.Shared.Security.KeyRing;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.MongoDb;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

builder.Services.AddValidation();
var appOptions = builder.Services.AddGroundControlOptions(builder.Configuration);

builder.Services.AddGroundControlMongo();

var dataProtectionBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("GroundControl");

var keyRingConfigurator = new FileSystemKeyRingConfigurator();
keyRingConfigurator.Configure(dataProtectionBuilder, builder.Configuration);
builder.Services.AddSingleton<IValueProtector, DataProtectionValueProtector>();
builder.Services.AddSingleton<IScopeResolver, ScopeResolver>();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new HeaderApiVersionReader("api-version");
});

var authConfigurator = appOptions.Security.AuthenticationMode switch
{
    AuthenticationMode.BuiltIn => throw new NotSupportedException("BuiltIn auth not yet implemented"),
    AuthenticationMode.External => throw new NotSupportedException("External auth not yet implemented"),
    _ => new NoAuthConfigurator()
};

authConfigurator.ConfigureServices(builder.Services, builder.Configuration);

new AuthenticationBuilder(builder.Services)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { });

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

builder.Services
    .AddAuthorizationBuilder()
    .AddPolicies(Permissions.All, "permission");

builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddCheck<MongoDbHealthCheck>("mongodb", failureStatus: HealthStatus.Unhealthy, tags: ["db", "mongodb", "ready"])
    .AddCheck<ChangeNotifierHealthCheck>("change-notifier", tags: ["ready"]);

var app = builder.Build();

authConfigurator.ConfigureMiddleware(app);
authConfigurator.MapEndpoints(app);
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

app.MapOpenApi();
app.MapHealthChecks("/healthz/liveness", new HealthCheckOptions { Predicate = p => p.Tags.Contains("liveness") });
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    Predicate = p => p.Tags.Contains("ready"),
    ResponseWriter = HealthCheckExtensions.WriteJsonResponse
});


app.Run();