using GroundControl.Host.Api;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace GroundControl.Api.Host.Modules;

internal sealed class ObservabilityModule : IWebApiModule
{
    private const string OtlpEndpointVariableName = "OTEL_EXPORTER_OTLP_ENDPOINT";

    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource("ground-control.api")
                    .AddAspNetCoreInstrumentation(options =>
                        // Exclude health check requests from tracing
                        options.Filter = context => !context.Request.Path.StartsWithSegments(HealthChecksModule.HealthEndpointPrefix, StringComparison.OrdinalIgnoreCase)
                    )
                    .AddHttpClientInstrumentation();
            });

        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointVariableName]);
        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }
    }
}