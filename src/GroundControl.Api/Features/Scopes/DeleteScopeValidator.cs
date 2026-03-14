using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Extensions.Http;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Scopes;

internal sealed class DeleteScopeValidator : IEndpointValidator
{
    private readonly IScopeStore _store;

    public DeleteScopeValidator(IScopeStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<ValidatorResult> ValidateAsync(ValidationContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HttpContext.Request.RouteValues.TryGetValue<Guid>("id", out var id))
        {
            return ValidatorResult.Problem("Route parameter 'id' is required.", StatusCodes.Status400BadRequest);
        }

        var scope = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (scope is null)
        {
            return ValidatorResult.Problem($"Scope '{id}' was not found.", StatusCodes.Status404NotFound);
        }

        if (!EntityTagHeaders.TryParseIfMatch(context.HttpContext, out _))
        {
            return ValidatorResult.Problem("If-Match header is required.", StatusCodes.Status428PreconditionRequired);
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
                return ValidatorResult.Problem(
                    $"Scope '{scope.Dimension}' cannot be deleted because value '{allowedValue}' is in use.",
                    StatusCodes.Status409Conflict);
            }
        }

        return ValidatorResult.Success;
    }
}