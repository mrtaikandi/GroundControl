namespace GroundControl.Cli.Features.ClientConfig.Get;

internal sealed class GetClientConfigOptions
{
    public Guid ClientId { get; set; }

    public string ClientSecret { get; set; } = string.Empty;
}