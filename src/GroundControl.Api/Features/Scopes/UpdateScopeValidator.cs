using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Scopes;

internal sealed class UpdateScopeValidator : IAsyncValidator<UpdateScopeRequest>
{
    private readonly IScopeStore _store;

    public UpdateScopeValidator(IScopeStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<ValidatorResult> ValidateAsync(UpdateScopeRequest instance, ValidationContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HttpContext.Request.RouteValues.TryGetValue<Guid>("id", out var id))
        {
            return ValidatorResult.Problem("Route parameter 'id' is required.", StatusCodes.Status400BadRequest);
        }

        var existingScope = await _store.GetByDimensionAsync(instance.Dimension, cancellationToken).ConfigureAwait(false);
        return existingScope is null || existingScope.Id == id
            ? ValidatorResult.Success
            : ValidatorResult.Problem($"A scope with dimension '{instance.Dimension}' already exists.", StatusCodes.Status409Conflict);
    }
}