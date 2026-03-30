using GroundControl.Api.Features.Auth.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security.Authentication;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Features.Auth;

internal sealed partial class TokenRefreshHandler : IEndpointHandler
{
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly AuthenticationOptions _options;
    private readonly ILogger<TokenRefreshHandler> _logger;

    public TokenRefreshHandler(
        IRefreshTokenStore refreshTokenStore,
        IOptions<AuthenticationOptions> options,
        ILogger<TokenRefreshHandler> logger)
    {
        _refreshTokenStore = refreshTokenStore ?? throw new ArgumentNullException(nameof(refreshTokenStore));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/token/refresh", async (
                RefreshRequest request,
                [FromServices] TokenRefreshHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(request, cancellationToken))
            .AllowAnonymous()
            .WithName(nameof(TokenRefreshHandler));
    }

    private async Task<IResult> HandleAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = TokenLoginHandler.ComputeTokenHash(request.RefreshToken);
        var existingToken = await _refreshTokenStore.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (existingToken is null)
        {
            return TypedResults.Problem(
                detail: "Invalid refresh token.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // Reuse detection: if token is already revoked, revoke the entire family
        if (existingToken.RevokedAt is not null)
        {
            LogReuseDetected(_logger, existingToken.FamilyId);
            await _refreshTokenStore.RevokeFamilyAsync(existingToken.FamilyId, DateTimeOffset.UtcNow, cancellationToken);

            return TypedResults.Problem(
                detail: "Refresh token has been revoked. Possible replay attack detected.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // Check expiration
        if (existingToken.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return TypedResults.Problem(
                detail: "Refresh token has expired.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var jwtOptions = _options.BuiltIn.Jwt;
        var accessToken = TokenLoginHandler.GenerateJwt(existingToken.UserId, jwtOptions);
        var (rawRefreshToken, newTokenHash) = TokenLoginHandler.GenerateRefreshToken();

        var now = DateTimeOffset.UtcNow;
        var newRefreshToken = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = existingToken.UserId,
            FamilyId = existingToken.FamilyId,
            TokenHash = newTokenHash,
            ExpiresAt = now.Add(jwtOptions.RefreshTokenLifetime),
            CreatedAt = now,
        };

        // Revoke old token and create new one
        await _refreshTokenStore.RevokeAsync(existingToken.Id, now, newRefreshToken.Id, cancellationToken);
        await _refreshTokenStore.CreateAsync(newRefreshToken, cancellationToken);

        return TypedResults.Ok(new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            ExpiresIn = (int)jwtOptions.AccessTokenLifetime.TotalSeconds,
        });
    }

    [LoggerMessage(1, LogLevel.Warning, "Refresh token reuse detected for family '{FamilyId}'. Revoking entire family.")]
    private static partial void LogReuseDetected(ILogger<TokenRefreshHandler> logger, Guid familyId);
}