using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Groups;

internal sealed class UpdateGroupValidator : IAsyncValidator<UpdateGroupRequest>
{
    private readonly IGroupStore _store;

    public UpdateGroupValidator(IGroupStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<ValidatorResult> ValidateAsync(UpdateGroupRequest instance, ValidationContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HttpContext.Request.RouteValues.TryGetValue<Guid>("id", out var id))
        {
            return ValidatorResult.Problem("Route parameter 'id' is required.", StatusCodes.Status400BadRequest);
        }

        var existingGroup = await _store.GetByNameAsync(instance.Name, cancellationToken).ConfigureAwait(false);
        return existingGroup is null || existingGroup.Id == id
            ? ValidatorResult.Success
            : ValidatorResult.Problem($"A group with name '{instance.Name}' already exists.", StatusCodes.Status409Conflict);
    }
}