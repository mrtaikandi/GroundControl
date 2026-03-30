namespace GroundControl.Api.Core.Authentication.NoAuth;

internal sealed class SeedOptions
{
    internal const string SectionName = $"{AuthenticationOptions.SectionName}:Seed";

    public string AdminUsername { get; set; } = "admin";

    public string AdminEmail { get; set; } = "admin@local";

    public string? AdminPassword { get; set; }
}