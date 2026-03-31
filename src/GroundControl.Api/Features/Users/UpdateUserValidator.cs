using GroundControl.Api.Features.Users.Contracts;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Users;

internal sealed class UpdateUserValidator : IAsyncValidator<UpdateUserRequest>
{
    private readonly IUserStore _userStore;

    public UpdateUserValidator(IUserStore userStore)
    {
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
    }

    public async Task<ValidatorResult> ValidateAsync(UpdateUserRequest instance, ValidationContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HttpContext.Request.RouteValues.TryGetValue<Guid>("id", out var id))
        {
            return ValidatorResult.Problem("Route parameter 'id' is required.", StatusCodes.Status400BadRequest);
        }

        var existingByEmail = await _userStore.GetByEmailAsync(instance.Email, cancellationToken).ConfigureAwait(false);
        if (existingByEmail is not null && existingByEmail.Id != id)
        {
            return ValidatorResult.Fail($"A user with email '{instance.Email}' already exists.", [nameof(UpdateUserRequest.Email)]);
        }

        var existingByUsername = await _userStore.GetByUsernameAsync(instance.Username, cancellationToken).ConfigureAwait(false);
        if (existingByUsername is not null && existingByUsername.Id != id)
        {
            return ValidatorResult.Fail($"A user with username '{instance.Username}' already exists.", [nameof(UpdateUserRequest.Username)]);
        }

        return ValidatorResult.Success;
    }
}