using GroundControl.Api.Shared.Observability;
using GroundControl.Host.Api;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace GroundControl.Api.Host.Modules;

[RunsAfter<ConfigurationModule>(Required = true)]
internal sealed class ObservabilityModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
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
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
    }
}