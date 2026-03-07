using Asp.Versioning;
using GroundControl.Api.Shared.Health;
using GroundControl.Persistence.MongoDb;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

builder.Services.AddValidation();
builder.Services.AddGroundControlMongo();
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new HeaderApiVersionReader("api-version");
});

builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddCheck<MongoDbHealthCheck>("mongodb", failureStatus: HealthStatus.Unhealthy, tags: ["db", "mongodb", "ready"])
    .AddCheck<ChangeNotifierHealthCheck>("change-notifier", tags: ["ready"]);

var app = builder.Build();

app.MapOpenApi();
app.MapHealthChecks("/healthz/liveness", new HealthCheckOptions { Predicate = p => p.Tags.Contains("liveness") });
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    Predicate = p => p.Tags.Contains("ready"),
    ResponseWriter = HealthCheckExtensions.WriteJsonResponse
});


app.Run();