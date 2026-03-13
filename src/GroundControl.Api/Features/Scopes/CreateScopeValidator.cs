using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Scopes;

internal sealed class CreateScopeValidator : IAsyncValidator<CreateScopeRequest>
{
    private readonly IScopeStore _store;

    public CreateScopeValidator(IScopeStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<IReadOnlyList<ValidationResult>> ValidateAsync(
        CreateScopeRequest instance,
        CancellationToken cancellationToken = default)
    {
        var existingScope = await _store.GetByDimensionAsync(instance.Dimension, cancellationToken).ConfigureAwait(false);
        if (existingScope is not null)
        {
            return [ValidationResult.Error($"A scope with dimension '{instance.Dimension}' already exists.", [nameof(instance.Dimension)])];
        }

        return [];
    }
}