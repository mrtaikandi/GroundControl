using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Scopes;

internal sealed class CreateScopeValidator : IAsyncValidator<CreateScopeRequest>
{
    private readonly IScopeStore _store;

    public CreateScopeValidator(IScopeStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<ValidatorResult> ValidateAsync(CreateScopeRequest instance, ValidationContext context, CancellationToken cancellationToken = default)
    {
        var existingScope = await _store.GetByDimensionAsync(instance.Dimension, cancellationToken).ConfigureAwait(false);
        return existingScope is null
            ? ValidatorResult.Success
            : ValidatorResult.Fail($"A scope with dimension '{instance.Dimension}' already exists.", nameof(instance.Dimension));
    }
}