using System.Security.Claims;
using System.Text;
using System.Text.Json;
using GroundControl.Api.Features.ClientApi.Contracts;
using GroundControl.Api.Core.ChangeNotification;
using GroundControl.Api.Shared.Observability;
using GroundControl.Api.Shared.Resolvers;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.ClientApi;

internal sealed class StreamConfigHandler : IEndpointHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SnapshotCache _cache;
    private readonly IScopeResolver _scopeResolver;
    private readonly IValueProtector _protector;
    private readonly IChangeNotifier _changeNotifier;
    private readonly IConfiguration _configuration;

    public StreamConfigHandler(
        SnapshotCache cache,
        IScopeResolver scopeResolver,
        IValueProtector protector,
        IChangeNotifier changeNotifier,
        IConfiguration configuration)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _scopeResolver = scopeResolver ?? throw new ArgumentNullException(nameof(scopeResolver));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _changeNotifier = changeNotifier ?? throw new ArgumentNullException(nameof(changeNotifier));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/config/stream", (
                HttpContext httpContext,
                [FromServices] StreamConfigHandler handler,
                CancellationToken cancellationToken = default) => handler.HandleAsync(httpContext, cancellationToken))
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = AuthenticationSchemes.ApiKey })
            .WithName(nameof(StreamConfigHandler));
    }

    private async Task HandleAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var projectIdClaim = httpContext.User.FindFirstValue("projectId");
        if (projectIdClaim is null || !Guid.TryParse(projectIdClaim, out var projectId))
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var clientScopes = ParseClientScopes(httpContext.User);

        // Set SSE headers
        httpContext.Response.Headers.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";
        await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);

        GroundControlMetrics.SseActiveConnections.Add(1);
        GroundControlMetrics.SseTotalConnections.Add(1);

        try
        {
            var lastEventId = httpContext.Request.Headers["Last-Event-ID"].FirstOrDefault();

            // Send initial config unless Last-Event-ID matches current snapshot
            var snapshot = await _cache.GetOrLoadAsync(projectId, cancellationToken).ConfigureAwait(false);
            if (snapshot is not null && !string.Equals(snapshot.Id.ToString(), lastEventId, StringComparison.Ordinal))
            {
                await WriteConfigEventAsync(httpContext.Response, snapshot, clientScopes, cancellationToken).ConfigureAwait(false);
            }

            var heartbeatInterval = _configuration.GetValue("ClientApi:HeartbeatIntervalSeconds", 30);
            using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(heartbeatInterval));

            // Race change notifications against heartbeat timer
            var changeStream = _changeNotifier.SubscribeAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
            await using (changeStream.ConfigureAwait(false))
            {
                Task<bool>? moveNextTask = null;
                Task<bool>? heartbeatTask = null;

                while (!cancellationToken.IsCancellationRequested)
                {
                    moveNextTask ??= changeStream.MoveNextAsync().AsTask();
                    heartbeatTask ??= heartbeatTimer.WaitForNextTickAsync(cancellationToken).AsTask();

                    var completed = await Task.WhenAny(moveNextTask, heartbeatTask).ConfigureAwait(false);

                    if (completed == moveNextTask)
                    {
                        if (!await moveNextTask.ConfigureAwait(false))
                        {
                            break;
                        }

                        moveNextTask = null;

                        var (changedProjectId, _) = changeStream.Current;
                        if (changedProjectId == projectId)
                        {
                            var updatedSnapshot = await _cache.InvalidateAsync(projectId, cancellationToken).ConfigureAwait(false);
                            if (updatedSnapshot is not null)
                            {
                                await WriteConfigEventAsync(httpContext.Response, updatedSnapshot, clientScopes, cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }

                    if (completed == heartbeatTask)
                    {
                        if (!await heartbeatTask.ConfigureAwait(false))
                        {
                            break;
                        }

                        heartbeatTask = null;
                        await WriteHeartbeatEventAsync(httpContext.Response, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected — exit cleanly
        }
        finally
        {
            GroundControlMetrics.SseActiveConnections.Add(-1);
        }
    }

    private async Task WriteConfigEventAsync(
        HttpResponse response,
        Snapshot snapshot,
        Dictionary<string, string> clientScopes,
        CancellationToken cancellationToken)
    {
        var data = ResolveConfig(snapshot, clientScopes);

        var payload = new ClientConfigResponse
        {
            Data = data,
            SnapshotId = snapshot.Id,
            SnapshotVersion = snapshot.SnapshotVersion,
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);

        var sb = new StringBuilder();
        sb.Append("event: config\n");
        sb.Append("id: ").Append(snapshot.Id).Append('\n');
        sb.Append("data: ").Append(json).Append('\n');
        sb.Append('\n');

        await response.WriteAsync(sb.ToString(), cancellationToken).ConfigureAwait(false);
        await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteHeartbeatEventAsync(HttpResponse response, CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("o");
        var sb = new StringBuilder();
        sb.Append("event: heartbeat\n");
        sb.Append("data: {\"timestamp\":\"").Append(timestamp).Append("\"}\n");
        sb.Append('\n');

        await response.WriteAsync(sb.ToString(), cancellationToken).ConfigureAwait(false);
        await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private Dictionary<string, string> ResolveConfig(Snapshot snapshot, Dictionary<string, string> clientScopes)
    {
        var data = new Dictionary<string, string>();
        foreach (var entry in snapshot.Entries)
        {
            var resolved = _scopeResolver.Resolve((IReadOnlyList<ScopedValue>)entry.Values, clientScopes);
            if (resolved is null)
            {
                continue;
            }

            var value = entry.IsSensitive ? _protector.Unprotect(resolved.Value) : resolved.Value;
            data[entry.Key] = value;
        }

        return data;
    }

    private static Dictionary<string, string> ParseClientScopes(ClaimsPrincipal principal)
    {
        var scopes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var claim in principal.FindAll("clientScope"))
        {
            var separatorIndex = claim.Value.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex > 0)
            {
                var key = claim.Value[..separatorIndex];
                var value = claim.Value[(separatorIndex + 1)..];
                scopes[key] = value;
            }
        }

        return scopes;
    }

}