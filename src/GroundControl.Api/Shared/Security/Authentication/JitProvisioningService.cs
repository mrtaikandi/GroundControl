using System.Security.Claims;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Shared.Security.Authentication;

internal sealed partial class JitProvisioningService
{
    private readonly IUserStore _userStore;
    private readonly JitProvisioningOptions _jitOptions;
    private readonly string _providerName;
    private readonly ILogger<JitProvisioningService> _logger;

    public JitProvisioningService(
        IUserStore userStore,
        ExternalAuthenticationOptions externalOptions,
        ILogger<JitProvisioningService> logger)
    {
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
        _jitOptions = externalOptions?.JitProvisioning ?? throw new ArgumentNullException(nameof(externalOptions));
        _providerName = externalOptions.ProviderName;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal async Task<JitProvisioningResult> ProvisionAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue("sub");

        if (string.IsNullOrEmpty(sub))
        {
            return JitProvisioningResult.Failure("OIDC token does not contain a subject identifier.");
        }

        var email = principal.FindFirstValue(ClaimTypes.Email)
                    ?? principal.FindFirstValue("email");

        var displayName = principal.FindFirstValue("name")
                          ?? principal.FindFirstValue(ClaimTypes.Name)
                          ?? email
                          ?? sub;

        // 1. Match by ExternalId (sub claim)
        var user = await _userStore.GetByExternalIdAsync(_providerName, sub, cancellationToken).ConfigureAwait(false);
        if (user is not null)
        {
            LogExternalIdMatched(_logger, sub, user.Id);
            return JitProvisioningResult.Success(user);
        }

        // 2. Match by email (if enabled and email is available)
        if (_jitOptions.MatchByEmail && !string.IsNullOrEmpty(email))
        {
            user = await _userStore.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);
            if (user is not null)
            {
                // Reject if already linked to a different external identity
                if (user.ExternalId is not null)
                {
                    LogEmailMatchRejected(_logger, email, user.Id);
                    return JitProvisioningResult.Failure(
                        "User is already linked to a different external identity.");
                }

                // Link external ID to existing user
                user.ExternalId = sub;
                user.ExternalProvider = _providerName;
                user.UpdatedAt = DateTimeOffset.UtcNow;
                user.UpdatedBy = user.Id;
                await _userStore.UpdateAsync(user, user.Version, cancellationToken).ConfigureAwait(false);
                LogExternalIdLinked(_logger, sub, user.Id, email);
                return JitProvisioningResult.Success(user);
            }
        }

        // 3. Auto-create (if enabled)
        if (_jitOptions.AutoCreate)
        {
            var newUser = new User
            {
                Id = Guid.CreateVersion7(),
                Username = email ?? sub,
                Email = email ?? $"{sub}@external",
                ExternalId = sub,
                ExternalProvider = _providerName,
                IsActive = true,
                Grants = [],
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = Guid.Empty,
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedBy = Guid.Empty
            };

            await _userStore.CreateAsync(newUser, cancellationToken).ConfigureAwait(false);
            LogUserProvisioned(_logger, newUser.Id, sub);
            return JitProvisioningResult.Success(newUser);
        }

        // 4. Reject
        return JitProvisioningResult.Failure("No matching user found and auto-creation is disabled.");
    }

    [LoggerMessage(1, LogLevel.Debug, "ExternalId {Sub} matched existing user {UserId}.")]
    private static partial void LogExternalIdMatched(ILogger logger, string sub, Guid userId);

    [LoggerMessage(2, LogLevel.Information, "Linked ExternalId {Sub} to existing user {UserId} matched by email {Email}.")]
    private static partial void LogExternalIdLinked(ILogger logger, string sub, Guid userId, string email);

    [LoggerMessage(3, LogLevel.Information, "JIT provisioned new user {UserId} for ExternalId {Sub}.")]
    private static partial void LogUserProvisioned(ILogger logger, Guid userId, string sub);

    [LoggerMessage(4, LogLevel.Warning, "Email {Email} matches user {UserId} but user is already linked to a different ExternalId.")]
    private static partial void LogEmailMatchRejected(ILogger logger, string email, Guid userId);
}

internal sealed class JitProvisioningResult
{
    private JitProvisioningResult(User? user, string? error)
    {
        User = user;
        Error = error;
    }

    internal User? User { get; }

    internal string? Error { get; }

    internal bool Succeeded => User is not null;

    internal static JitProvisioningResult Success(User user) => new(user, null);

    internal static JitProvisioningResult Failure(string error) => new(null, error);
}