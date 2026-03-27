namespace GroundControl.Api.Shared.Security;

internal sealed class ExternalSecurityOptions
{
    public string Authority { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string? ClientSecret { get; set; }

    public string ResponseType { get; set; } = "code";

    public string[] Scopes { get; set; } = ["openid", "profile", "email"];

    public string CallbackPath { get; set; } = "/signin-oidc";

    public string? Audience { get; set; }

    public string ProviderName { get; set; } = "oidc";

    public JitProvisioningOptions JitProvisioning { get; set; } = new();

    public ExternalCookieOptions Cookie { get; set; } = new();
}

internal sealed class JitProvisioningOptions
{
    public bool Enabled { get; set; } = true;

    public bool MatchByEmail { get; set; } = true;

    public bool AutoCreate { get; set; } = true;
}

internal sealed class ExternalCookieOptions
{
    public string Name { get; set; } = ".GroundControl.Auth";

    public TimeSpan ExpireTimeSpan { get; set; } = TimeSpan.FromDays(14);

    public bool SlidingExpiration { get; set; } = true;
}