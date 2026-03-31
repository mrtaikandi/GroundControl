using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using AspNetCore.Identity.MongoDbCore.Models;
using GroundControl.Api.Core.Authentication.Contracts;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using JwtOptions = GroundControl.Api.Core.Authentication.BuiltIn.JwtOptions;

namespace GroundControl.Api.Core.Authentication;

internal sealed class TokenLoginHandler : IEndpointHandler
{
    private readonly UserManager<MongoIdentityUser<Guid>> _userManager;
    private readonly SignInManager<MongoIdentityUser<Guid>> _signInManager;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly AuthenticationOptions _options;

    public TokenLoginHandler(
        UserManager<MongoIdentityUser<Guid>> userManager,
        SignInManager<MongoIdentityUser<Guid>> signInManager,
        IRefreshTokenStore refreshTokenStore,
        IOptions<AuthenticationOptions> options)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
        _refreshTokenStore = refreshTokenStore ?? throw new ArgumentNullException(nameof(refreshTokenStore));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/token", async (
                LoginRequest request,
                [FromServices] TokenLoginHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(request, cancellationToken))
            .AllowAnonymous()
            .WithName(nameof(TokenLoginHandler));
    }

    private async Task<IResult> HandleAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _signInManager.PasswordSignInAsync(
            request.Username, request.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return TypedResults.Problem(
                detail: "Account is locked out due to too many failed attempts. Try again later.",
                statusCode: StatusCodes.Status423Locked);
        }

        if (!result.Succeeded)
        {
            return TypedResults.Problem(
                detail: "Invalid username or password.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var identityUser = await _userManager.FindByNameAsync(request.Username);
        if (identityUser is null)
        {
            return TypedResults.Problem(
                detail: "User account not found.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var jwtOptions = _options.BuiltIn.Jwt;
        var accessToken = GenerateJwt(identityUser.Id, jwtOptions);
        var (rawRefreshToken, tokenHash) = GenerateRefreshToken();

        var now = DateTimeOffset.UtcNow;
        var refreshToken = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = identityUser.Id,
            FamilyId = Guid.CreateVersion7(),
            TokenHash = tokenHash,
            ExpiresAt = now.Add(jwtOptions.RefreshTokenLifetime),
            CreatedAt = now,
        };

        await _refreshTokenStore.CreateAsync(refreshToken, cancellationToken);

        return TypedResults.Ok(new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            ExpiresIn = (int)jwtOptions.AccessTokenLifetime.TotalSeconds,
        });
    }

    internal static string GenerateJwt(Guid userId, JwtOptions jwtOptions)
    {
        var key = new SymmetricSecurityKey(Convert.FromBase64String(jwtOptions.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            expires: DateTime.UtcNow.Add(jwtOptions.AccessTokenLifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    internal static (string RawToken, string TokenHash) GenerateRefreshToken()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(tokenBytes);
        var hashBytes = SHA256.HashData(tokenBytes);
        var tokenHash = Convert.ToBase64String(hashBytes);

        return (rawToken, tokenHash);
    }

    internal static string ComputeTokenHash(string rawToken)
    {
        var tokenBytes = Convert.FromBase64String(rawToken);
        var hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToBase64String(hashBytes);
    }
}