using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Scopes;

internal sealed class CreateScopeHandler : IEndpointHandler
{
    private readonly IScopeStore _store;

    public CreateScopeHandler(IScopeStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(string.Empty, async (
                CreateScopeRequest request,
                [FromServices] CreateScopeHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(request, cancellationToken))
            .RequireAuthorization(Permissions.ScopesWrite)
            .WithName(nameof(CreateScopeHandler));
    }

    private async Task<IResult> HandleAsync(CreateScopeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existingScope = await _store.GetByDimensionAsync(request.Dimension, cancellationToken).ConfigureAwait(false);
        if (existingScope is not null)
        {
            return TypedResults.Problem(
                detail: $"A scope with dimension '{request.Dimension}' already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var timestamp = DateTimeOffset.UtcNow;
        var scope = new Scope
        {
            Id = Guid.CreateVersion7(),
            Dimension = request.Dimension,
            AllowedValues = [.. request.AllowedValues],
            Description = request.Description,
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty,
        };

        await _store.CreateAsync(scope, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created($"/api/scopes/{scope.Id}", ScopeResponse.From(scope));
    }
}