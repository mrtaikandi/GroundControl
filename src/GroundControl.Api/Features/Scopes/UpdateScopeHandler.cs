using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Scopes;

internal sealed class UpdateScopeHandler : IEndpointHandler
{
    private readonly IScopeStore _store;

    public UpdateScopeHandler(IScopeStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}", async (
                Guid id,
                UpdateScopeRequest request,
                HttpContext httpContext,
                [FromServices] UpdateScopeHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, request, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.ScopesWrite)
            .WithName(nameof(UpdateScopeHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, UpdateScopeRequest request, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
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

        var existingScope = await _store.GetByDimensionAsync(request.Dimension, cancellationToken).ConfigureAwait(false);
        if (existingScope is not null && existingScope.Id != scope.Id)
        {
            return TypedResults.Problem(
                detail: $"A scope with dimension '{request.Dimension}' already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        scope.Dimension = request.Dimension;
        scope.AllowedValues.Clear();

        foreach (var allowedValue in request.AllowedValues)
        {
            scope.AllowedValues.Add(allowedValue);
        }

        scope.Description = request.Description;
        scope.UpdatedAt = DateTimeOffset.UtcNow;
        scope.UpdatedBy = Guid.Empty;

        var updated = await _store.UpdateAsync(scope, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(scope.Version);
        return TypedResults.Ok(ScopeResponse.From(scope));
    }
}