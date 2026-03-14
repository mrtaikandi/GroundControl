using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Extensions.Http;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Roles;

internal sealed class DeleteRoleValidator : IEndpointValidator
{
    private readonly IRoleStore _store;

    public DeleteRoleValidator(IRoleStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<ValidatorResult> ValidateAsync(ValidationContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HttpContext.Request.RouteValues.TryGetValue<Guid>("id", out var id))
        {
            return ValidatorResult.Problem("Route parameter 'id' is required.", StatusCodes.Status400BadRequest);
        }

        var role = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return ValidatorResult.Problem($"Role '{id}' was not found.", StatusCodes.Status404NotFound);
        }

        var ifMatchResult = EntityTagHeaders.ValidateIfMatch(context.HttpContext);
        if (ifMatchResult.IsFailed)
        {
            return ifMatchResult;
        }

        var isReferenced = await _store.IsReferencedByUsersAsync(id, cancellationToken).ConfigureAwait(false);
        if (isReferenced)
        {
            return ValidatorResult.Problem(
                $"Role '{role.Name}' cannot be deleted because it is referenced by one or more users.",
                StatusCodes.Status409Conflict);
        }

        return ValidatorResult.Success;
    }
}