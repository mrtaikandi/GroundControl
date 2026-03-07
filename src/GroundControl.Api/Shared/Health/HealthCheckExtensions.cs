using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GroundControl.Api.Shared.Health;

/// <summary>
/// Provides extension methods for <see cref="Exception"/> instances.
/// </summary>
internal static class HealthCheckExtensions
{
    public static async Task WriteJsonResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var result = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            })
        };

        await context.Response.WriteAsJsonAsync(result, cancellationToken: context.RequestAborted);
    }

    /// <summary>
    /// Returns a short one-line representation of the exception including its type and message.
    /// If an inner exception exists, its message is appended using an arrow separator.
    /// </summary>
    /// <param name="exception">The exception to format.</param>
    /// <returns>A short string describing the exception.</returns>
    public static string ToShortString(this Exception exception) => exception.InnerException is not null
        ? $"[{exception.GetType().Name}] {exception.Message} -> {exception.InnerException.Message}"
        : $"[{exception.GetType().Name}] {exception.Message}";
}