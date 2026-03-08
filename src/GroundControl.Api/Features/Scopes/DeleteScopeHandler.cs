using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Scopes;

internal sealed class DeleteScopeHandler : IEndpointHandler
{
    private readonly IScopeStore _store;

    public DeleteScopeHandler(IScopeStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                [FromServices] DeleteScopeHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.ScopesWrite)
            .WithName(nameof(DeleteScopeHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var scope = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (scope is null)
        {
            return TypedResults.Problem(detail: $"Scope '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion))
        {
            return TypedResults.Problem(detail: "If-Match header is required.", statusCode: StatusCodes.Status428PreconditionRequired);
        }

        var inspectedValues = new HashSet<string>(StringComparer.Ordinal);
        foreach (var allowedValue in scope.AllowedValues)
        {
            if (!inspectedValues.Add(allowedValue))
            {
                continue;
            }

            var isReferenced = await _store.IsReferencedAsync(scope.Dimension, allowedValue, cancellationToken).ConfigureAwait(false);
            if (isReferenced)
            {
                return TypedResults.Problem(
                    detail: $"Scope '{scope.Dimension}' cannot be deleted because value '{allowedValue}' is in use.",
                    statusCode: StatusCodes.Status409Conflict);
            }
        }

        var deleted = await _store.DeleteAsync(id, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        return TypedResults.NoContent();
    }
}