using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;
using ValidationContext = GroundControl.Api.Shared.Validation.ValidationContext;

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
            : ValidatorResult.ValidationProblem(ValidationResult.Error($"A scope with dimension '{instance.Dimension}' already exists.", [nameof(instance.Dimension)]));
    }
}