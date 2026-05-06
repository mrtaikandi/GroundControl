using GroundControl.Api.Shared.Activity;

namespace GroundControl.Api.Features.Activity.Contracts;

internal sealed record ActivitySummaryResponse
{
    public required int Clients { get; init; }

    public required double Rate { get; init; }

    public static ActivitySummaryResponse From(LiveActivitySnapshot snapshot) => new()
    {
        Clients = snapshot.Clients,
        Rate = snapshot.Rate,
    };
}