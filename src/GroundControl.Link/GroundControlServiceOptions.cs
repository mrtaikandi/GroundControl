using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace GroundControl.Link;

/// <summary>
/// Options for Phase 2 DI service registration.
/// </summary>
public sealed class GroundControlServiceOptions
{
    /// <summary>
    /// Gets or sets health check tags. Defaults to <c>["ready"]</c>.
    /// </summary>
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Simple mutable options bag for consumer convenience")]
    public string[] HealthCheckTags { get; set; } = ["ready"];

    /// <summary>
    /// Gets or sets an optional delegate to customize the named "GroundControl" <see cref="HttpClient"/>.
    /// Use this to add delegating handlers, retry policies, or other <see cref="IHttpClientBuilder"/> middleware.
    /// </summary>
    public Action<IHttpClientBuilder>? ConfigureHttpClient { get; set; }
}