using Microsoft.AspNetCore.Antiforgery;

namespace GroundControl.Api.Core.Authentication.External;

/// <summary>
/// Middleware that enforces CSRF protection for cookie-authenticated state-changing requests
/// using the double-submit cookie pattern. Bearer/PAT requests are exempt.
/// </summary>
internal sealed partial class CsrfProtectionMiddleware
{
    private static readonly HashSet<string> StateChangingMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "DELETE", "PATCH" };

    private readonly RequestDelegate _next;
    private readonly IAntiforgery _antiforgery;
    private readonly CsrfOptions _options;
    private readonly ILogger<CsrfProtectionMiddleware> _logger;

    public CsrfProtectionMiddleware(
        RequestDelegate next,
        IAntiforgery antiforgery,
        CsrfOptions options,
        ILogger<CsrfProtectionMiddleware> logger)
    {
        _next = next;
        _antiforgery = antiforgery;
        _options = options;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (IsCookieAuthenticated(context) && IsStateChangingApiRequest(context))
        {
            if (!await _antiforgery.IsRequestValidAsync(context).ConfigureAwait(false))
            {
                LogCsrfValidationFailure(
                    _logger,
                    context.Request.Method,
                    context.Request.Path,
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

                var result = Results.Problem(
                    detail: "The CSRF token is missing or invalid. Include the XSRF-TOKEN cookie value in the X-XSRF-TOKEN header.",
                    title: "CSRF token validation failed.",
                    statusCode: StatusCodes.Status403Forbidden,
                    type: "https://tools.ietf.org/html/rfc9110#section-15.5.4");

                await result.ExecuteAsync(context).ConfigureAwait(false);
                return;
            }
        }

        // Issue CSRF token cookie for cookie-authenticated users so subsequent requests can include it
        if (IsCookieAuthenticated(context))
        {
            var tokens = _antiforgery.GetAndStoreTokens(context);
            if (tokens.RequestToken is not null)
            {
                context.Response.Cookies.Append(_options.CookieName, tokens.RequestToken, new CookieOptions
                {
                    HttpOnly = false,
                    SameSite = SameSiteMode.Strict,
                    Secure = context.Request.IsHttps,
                    IsEssential = true
                });
            }
        }

        await _next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Determines if the request is authenticated via cookies (no Authorization header present).
    /// Bearer/PAT requests always include an Authorization header and are exempt from CSRF.
    /// </summary>
    private static bool IsCookieAuthenticated(HttpContext context) =>
        context.User.Identity is { IsAuthenticated: true } &&
        string.IsNullOrEmpty(context.Request.Headers.Authorization.ToString());

    private static bool IsStateChangingApiRequest(HttpContext context) =>
        StateChangingMethods.Contains(context.Request.Method) &&
        context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

    [LoggerMessage(1, LogLevel.Warning, "CSRF validation failed for {Method} {Path} from {RemoteIp}.")]
    private static partial void LogCsrfValidationFailure(ILogger logger, string method, string path, string remoteIp);
}