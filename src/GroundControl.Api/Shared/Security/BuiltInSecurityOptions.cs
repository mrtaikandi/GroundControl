using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Shared.Security;

internal sealed class BuiltInSecurityOptions
{
    public JwtOptions Jwt { get; set; } = new();

    public BuiltInCookieOptions Cookie { get; set; } = new();

    public PasswordPolicyOptions Password { get; set; } = new();

    public LockoutPolicyOptions Lockout { get; set; } = new();
}

internal sealed class JwtOptions
{
    [Required(AllowEmptyStrings = false)]
    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "GroundControl";

    public string Audience { get; set; } = "GroundControl";

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);
}

internal sealed class BuiltInCookieOptions
{
    public string Name { get; set; } = ".GroundControl.Auth";

    public TimeSpan ExpireTimeSpan { get; set; } = TimeSpan.FromDays(14);

    public bool SlidingExpiration { get; set; } = true;
}

internal sealed class PasswordPolicyOptions
{
    public int RequiredLength { get; set; } = 12;

    public bool RequireDigit { get; set; } = true;

    public bool RequireUppercase { get; set; } = true;

    public bool RequireLowercase { get; set; } = true;

    public bool RequireNonAlphanumeric { get; set; }
}

internal sealed class LockoutPolicyOptions
{
    public int MaxFailedAttempts { get; set; } = 5;

    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
}