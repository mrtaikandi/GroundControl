using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Core.Authentication.External;

internal sealed partial class ExternalAuthenticationOptions
{
    internal const string SectionName = $"{AuthenticationOptions.SectionName}:External";

    [Required(AllowEmptyStrings = false)]
    public string Authority { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string ClientId { get; set; } = string.Empty;

    public string? ClientSecret { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string ResponseType { get; set; } = "code";

    [Required(AllowEmptyStrings = false)]
    public string[] Scopes { get; set; } = ["openid", "profile", "email"];

    [Required(AllowEmptyStrings = false)]
    public string CallbackPath { get; set; } = "/signin-oidc";

    public string? Audience { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string ProviderName { get; set; } = "oidc";

    public JitProvisioningOptions JitProvisioning { get; set; } = new();

    [ValidateObjectMembers]
    public ExternalCookieOptions Cookie { get; set; } = new();

    [OptionsValidator]
    public sealed partial class Validator : IValidateOptions<ExternalAuthenticationOptions>;
}

internal sealed class JitProvisioningOptions
{
    internal const string SectionName = $"{ExternalAuthenticationOptions.SectionName}:JitProvisioning";

    public bool Enabled { get; set; } = true;

    public bool MatchByEmail { get; set; } = true;

    public bool AutoCreate { get; set; } = true;
}

internal sealed partial class ExternalCookieOptions
{
    internal const string SectionName = $"{ExternalAuthenticationOptions.SectionName}:Cookie";

    [Required(AllowEmptyStrings = false)]
    public string Name { get; set; } = ".GroundControl.Auth";

    public TimeSpan ExpireTimeSpan { get; set; } = TimeSpan.FromDays(14);

    public bool SlidingExpiration { get; set; } = true;

    [OptionsValidator]
    public sealed partial class Validator : IValidateOptions<ExternalCookieOptions>;
}