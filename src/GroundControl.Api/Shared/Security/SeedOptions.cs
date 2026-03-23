namespace GroundControl.Api.Shared.Security;

internal sealed class SeedOptions
{
    public string AdminUsername { get; set; } = "admin";

    public string AdminEmail { get; set; } = "admin@local";

    public string? AdminPassword { get; set; }
}