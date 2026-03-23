using GroundControl.Api.Features.Users.Contracts;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Users;

internal sealed class CreateUserValidator : IAsyncValidator<CreateUserRequest>
{
    private readonly IUserStore _userStore;

    public CreateUserValidator(IUserStore userStore)
    {
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
    }

    public async Task<ValidatorResult> ValidateAsync(CreateUserRequest instance, ValidationContext context, CancellationToken cancellationToken = default)
    {
        var existingByEmail = await _userStore.GetByEmailAsync(instance.Email, cancellationToken).ConfigureAwait(false);
        if (existingByEmail is not null)
        {
            return ValidatorResult.Fail($"A user with email '{instance.Email}' already exists.", [nameof(CreateUserRequest.Email)]);
        }

        var existingByUsername = await _userStore.GetByUsernameAsync(instance.Username, cancellationToken).ConfigureAwait(false);
        if (existingByUsername is not null)
        {
            return ValidatorResult.Fail($"A user with username '{instance.Username}' already exists.", [nameof(CreateUserRequest.Username)]);
        }

        return ValidatorResult.Success;
    }
}