namespace GroundControl.Api.Core;

/// <summary>
/// Defines a vertical slice endpoint handler that maps its routes to the application.
/// </summary>
internal interface IEndpointHandler
{
    /// <summary>
    /// Maps the endpoint routes for this handler.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    abstract static void Endpoint(IEndpointRouteBuilder endpoints);
}