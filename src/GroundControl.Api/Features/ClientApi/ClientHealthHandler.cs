
namespace GroundControl.Api.Features.ClientApi;

internal sealed class ClientHealthHandler : IEndpointHandler
{
    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", () => TypedResults.Ok())
            .AllowAnonymous()
            .WithSummary("Client health check")
            .WithDescription("Returns OK to indicate the client API is reachable.")
            .Produces(StatusCodes.Status200OK)
            .WithName(nameof(ClientHealthHandler));
    }
}