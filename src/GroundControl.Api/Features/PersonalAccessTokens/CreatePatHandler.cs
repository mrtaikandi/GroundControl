using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GroundControl.Api.Features.PersonalAccessTokens.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.PersonalAccessTokens;

internal sealed class CreatePatHandler : IEndpointHandler
{
    private readonly IPersonalAccessTokenStore _store;
    private readonly AuditRecorder _audit;

    public CreatePatHandler(IPersonalAccessTokenStore store, AuditRecorder audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(string.Empty, async (
                CreatePatRequest request,
                [FromServices] CreatePatHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(request, httpContext, cancellationToken))
            .RequireAuthorization()
            .WithContractValidation<CreatePatRequest>()
            .WithSummary("Create a personal access token")
            .WithDescription("Generates a new personal access token for API authentication. The raw token is only returned once.")
            .Produces<CreatePatResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName(nameof(CreatePatHandler));
    }

    private async Task<IResult> HandleAsync(
        CreatePatRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return TypedResults.Problem(
                detail: "Unable to determine user identity.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = $"gc_pat_{Base64UrlEncode(rawBytes)}";

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        var tokenHash = Convert.ToHexStringLower(hashBytes);

        var tokenPrefix = rawToken[7..15]; // 8 chars after "gc_pat_"
        var timestamp = DateTimeOffset.UtcNow;

        var pat = new PersonalAccessToken
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = request.Name,
            TokenPrefix = tokenPrefix,
            TokenHash = tokenHash,
            Permissions = request.Permissions is not null ? [.. request.Permissions] : null,
            ExpiresAt = request.ExpiresInDays.HasValue
                ? timestamp.AddDays(request.ExpiresInDays.Value)
                : null,
            CreatedAt = timestamp,
        };

        await _store.CreateAsync(pat, cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync("PersonalAccessToken", pat.Id, null, "Created", cancellationToken: cancellationToken).ConfigureAwait(false);

        var response = new CreatePatResponse
        {
            Id = pat.Id,
            Name = pat.Name,
            Token = rawToken,
            TokenPrefix = pat.TokenPrefix,
            Permissions = request.Permissions is not null ? [.. request.Permissions] : null,
            ExpiresAt = pat.ExpiresAt,
            CreatedAt = pat.CreatedAt,
        };

        return TypedResults.Created($"/api/personal-access-tokens/{pat.Id}", response);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}