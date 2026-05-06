using GroundControl.Api.Features.Activity.Contracts;
using GroundControl.Api.Shared.Activity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Activity;

internal sealed class GetActivitySummaryHandler : IEndpointHandler
{
    private readonly ILiveActivityTracker _tracker;

    public GetActivitySummaryHandler(ILiveActivityTracker tracker)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/summary", ([FromServices] GetActivitySummaryHandler handler) => handler.Handle())
            .WithSummary("Get live activity summary")
            .WithDescription("Returns the current live client count and activity event rate.")
            .Produces<ActivitySummaryResponse>()
            .WithName(nameof(GetActivitySummaryHandler));
    }

    private Ok<ActivitySummaryResponse> Handle() => TypedResults.Ok(ActivitySummaryResponse.From(_tracker.Current));
}