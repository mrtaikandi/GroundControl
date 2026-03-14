using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Extensions.Http;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Groups;

internal sealed class DeleteGroupValidator : IEndpointValidator
{
    private readonly IGroupStore _store;

    public DeleteGroupValidator(IGroupStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<ValidatorResult> ValidateAsync(ValidationContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HttpContext.Request.RouteValues.TryGetValue<Guid>("id", out var id))
        {
            return ValidatorResult.Problem("Route parameter 'id' is required.", StatusCodes.Status400BadRequest);
        }

        var group = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return ValidatorResult.Problem($"Group '{id}' was not found.", StatusCodes.Status404NotFound);
        }

        if (!EntityTagHeaders.TryParseIfMatch(context.HttpContext, out _))
        {
            return ValidatorResult.Problem("If-Match header is required.", StatusCodes.Status428PreconditionRequired);
        }

        var hasDependents = await _store.HasDependentsAsync(id, cancellationToken).ConfigureAwait(false);
        if (hasDependents)
        {
            return ValidatorResult.Problem(
                $"Group '{group.Name}' cannot be deleted because it has dependent entities.",
                StatusCodes.Status409Conflict);
        }

        return ValidatorResult.Success;
    }
}