using GroundControl.Api.Shared;

namespace GroundControl.Api.Features.ClientApi;

internal sealed class ClientHealthHandler : IEndpointHandler
{
    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", () => TypedResults.Ok())
            .AllowAnonymous()
            .WithName(nameof(ClientHealthHandler));
    }
}