using System.ComponentModel.DataAnnotations;
using GroundControl.Host.Api;
using ValidationContext = System.ComponentModel.DataAnnotations.ValidationContext;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Core.Authentication.BuiltIn;

internal sealed partial class BuiltInAuthenticationOptions
{
    internal const string SectionName = "Authentication:BuiltIn";

    [ValidateObjectMembers]
    public JwtOptions Jwt { get; set; } = new();

    [ValidateObjectMembers]
    public BuiltInCookieOptions Cookie { get; set; } = new();

    public PasswordPolicyOptions Password { get; set; } = new();

    public LockoutPolicyOptions Lockout { get; set; } = new();

    [Required(AllowEmptyStrings = false)]
    [ConfigurationKey("ConnectionStrings:Storage", IsFallback = true)]
    public string ConnectionString { get; set; } = string.Empty;

    [OptionsValidator]
    public sealed partial class Validator : IValidateOptions<BuiltInAuthenticationOptions>;
}

internal sealed partial class JwtOptions
{
    internal const string SectionName = $"{BuiltInAuthenticationOptions.SectionName}:Jwt";

    [RequireValidJwtSecret]
    [Required(AllowEmptyStrings = false)]
    public string Secret { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; set; } = "GroundControl";

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; set; } = "GroundControl";

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);

    [OptionsValidator]
    public sealed partial class Validator : IValidateOptions<BuiltInAuthenticationOptions>;
}

internal sealed partial class BuiltInCookieOptions
{
    internal const string SectionName = $"{BuiltInAuthenticationOptions.SectionName}:Cookie";

    [Required(AllowEmptyStrings = false)]
    public string Name { get; set; } = ".GroundControl.Auth";

    public TimeSpan ExpireTimeSpan { get; set; } = TimeSpan.FromDays(14);

    public bool SlidingExpiration { get; set; } = true;

    [OptionsValidator]
    public sealed partial class Validator : IValidateOptions<BuiltInCookieOptions>;
}

internal sealed class PasswordPolicyOptions
{
    internal const string SectionName = $"{BuiltInAuthenticationOptions.SectionName}:Password";

    public int RequiredLength { get; set; } = 12;

    public bool RequireDigit { get; set; } = true;

    public bool RequireUppercase { get; set; } = true;

    public bool RequireLowercase { get; set; } = true;

    public bool RequireNonAlphanumeric { get; set; }
}

internal sealed class LockoutPolicyOptions
{
    internal const string SectionName = $"{BuiltInAuthenticationOptions.SectionName}:Lockout";

    public int MaxFailedAttempts { get; set; } = 5;

    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
}


[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
internal sealed class RequireValidJwtSecretAttribute : ValidationAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string secret || string.IsNullOrWhiteSpace(secret))
        {
            return ValidationResult.Success; // [Required] should handle nulls
        }

        try
        {
            var keyBytes = Convert.FromBase64String(secret);
            if (keyBytes.Length < 32)
            {
                return new ValidationResult(
                    "JWT signing key must be at least 256 bits (32 bytes). The configured key is too short.");
            }
        }
        catch (FormatException)
        {
            return new ValidationResult(
                $"The field {validationContext.DisplayName} must be a valid Base64-encoded string.");
        }

        return ValidationResult.Success;
    }
}