using Asp.Versioning;
using GroundControl.Api.Features.Groups;
using GroundControl.Api.Features.Roles;
using GroundControl.Api.Features.Scopes;
using GroundControl.Api.Shared.Configuration;
using GroundControl.Api.Shared.Health;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Auth;
using GroundControl.Persistence.MongoDb;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

builder.Services.AddValidation();
var appOptions = builder.Services.AddGroundControlOptions(builder.Configuration);

builder.Services.AddGroundControlMongo();
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
builder.Services.AddScopesHandlers();
builder.Services.AddGroupsHandlers();
builder.Services.AddRolesHandlers();

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

app.MapOpenApi();
app.MapHealthChecks("/healthz/liveness", new HealthCheckOptions { Predicate = p => p.Tags.Contains("liveness") });
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    Predicate = p => p.Tags.Contains("ready"),
    ResponseWriter = HealthCheckExtensions.WriteJsonResponse
});


app.Run();