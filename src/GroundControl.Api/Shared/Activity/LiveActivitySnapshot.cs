namespace GroundControl.Api.Shared.Activity;

internal sealed record LiveActivitySnapshot
{
    public required int Clients { get; init; }

    public required double Rate { get; init; }
}