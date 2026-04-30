using System.Text;
using System.Text.Json;
using GroundControl.Api.Features.Activity.Contracts;
using GroundControl.Api.Shared.Activity;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Activity;

internal sealed class StreamActivityHandler : IEndpointHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILiveActivityTracker _tracker;

    public StreamActivityHandler(ILiveActivityTracker tracker)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/stream", (
                HttpContext httpContext,
                [FromServices] StreamActivityHandler handler,
                CancellationToken cancellationToken = default) => handler.HandleAsync(httpContext, cancellationToken))
            .WithSummary("Stream live activity")
            .WithDescription("Opens a Server-Sent Events stream that emits live Tower activity telemetry.")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithName(nameof(StreamActivityHandler));
    }

    private async Task HandleAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        httpContext.Response.Headers.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";
        await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await foreach (var snapshot in _tracker.SubscribeAsync(cancellationToken).ConfigureAwait(false))
            {
                await WriteActivityEventAsync(httpContext.Response, snapshot, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task WriteActivityEventAsync(HttpResponse response, LiveActivitySnapshot snapshot, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(ActivitySummaryResponse.From(snapshot), JsonOptions);
        var sb = new StringBuilder();
        sb.Append("event: activity\n");
        sb.Append("data: ").Append(json).Append('\n');
        sb.Append('\n');

        await response.WriteAsync(sb.ToString(), cancellationToken).ConfigureAwait(false);
        await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}