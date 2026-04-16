using GroundControl.Link;
using GroundControl.Samples.LinkConsole;
using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

// 1: Register GroundControl as a configuration source.
// Options are bound from appsettings.json / environment variables / user secrets.
builder.Configuration.AddGroundControl(options =>
{
    options.ServerUrl = builder.Configuration.GetValue<Uri>("GroundControl:ServerUrl") ?? throw new InvalidOperationException("GroundControl:ServerUrl is required");
    options.ClientId = builder.Configuration.GetValue<string>("GroundControl:ClientId") ?? throw new InvalidOperationException("GroundControl:ClientId is required");
    options.ClientSecret = builder.Configuration.GetValue<string>("GroundControl:ClientSecret") ?? throw new InvalidOperationException("GroundControl:ClientSecret is required");
    options.ConnectionMode = builder.Configuration.GetValue("GroundControl:ConnectionMode", ConnectionMode.SseWithPollingFallback);
});

// 2: Register background services (SSE/polling), health check, and metrics.
builder.Services.AddGroundControl(builder.Configuration);

// Bind strongly-typed settings from the "Sample" section delivered by GroundControl and
// Register the configuration monitor that logs changes to the console.
builder.Services.Configure<SampleSettings>(builder.Configuration.GetSection("Sample"));
builder.Services.AddHostedService<ConfigurationMonitorService>();

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter("GroundControl.Link");
    })
    .WithTracing(tracing =>
    {
        tracing.AddHttpClientInstrumentation();
    });

if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Services.AddOpenTelemetry().UseOtlpExporter();
}

var app = builder.Build();
app.Run();