using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Scopes;

internal sealed class GetScopeHandler : IEndpointHandler
{
    private readonly IScopeStore _store;

    public GetScopeHandler(IScopeStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                [FromServices] GetScopeHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.ScopesRead)
            .WithSummary("Get a scope")
            .WithDescription("Returns a scope by its unique identifier. Includes an ETag header for optimistic concurrency.")
            .Produces<ScopeResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName(nameof(GetScopeHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var scope = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (scope is null)
        {
            return TypedResults.Problem(detail: $"Scope '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(scope.Version);
        return TypedResults.Ok(ScopeResponse.From(scope));
    }
}