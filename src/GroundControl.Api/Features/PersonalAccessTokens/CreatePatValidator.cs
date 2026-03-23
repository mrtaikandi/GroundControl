using GroundControl.Api.Features.PersonalAccessTokens.Contracts;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Validation;

namespace GroundControl.Api.Features.PersonalAccessTokens;

internal sealed class CreatePatValidator : IAsyncValidator<CreatePatRequest>
{
    public Task<ValidatorResult> ValidateAsync(CreatePatRequest instance, ValidationContext context, CancellationToken cancellationToken = default)
    {
        if (instance.Permissions is { Count: > 0 })
        {
            var invalidPermissions = instance.Permissions.Where(p => !Permissions.All.Contains(p)).ToList();
            if (invalidPermissions.Count > 0)
            {
                return Task.FromResult(
                    ValidatorResult.Fail($"Invalid permission(s): {string.Join(", ", invalidPermissions)}.", nameof(instance.Permissions)));
            }
        }

        return Task.FromResult(ValidatorResult.Success);
    }
}