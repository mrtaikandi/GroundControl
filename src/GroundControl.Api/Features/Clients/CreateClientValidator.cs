using GroundControl.Api.Features.Clients.Contracts;
using GroundControl.Api.Shared.Extensions.Http;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Clients;

internal sealed class CreateClientValidator : IAsyncValidator<CreateClientRequest>
{
    private readonly IProjectStore _projectStore;
    private readonly IScopeStore _scopeStore;

    public CreateClientValidator(IProjectStore projectStore, IScopeStore scopeStore)
    {
        _projectStore = projectStore ?? throw new ArgumentNullException(nameof(projectStore));
        _scopeStore = scopeStore ?? throw new ArgumentNullException(nameof(scopeStore));
    }

    public async Task<ValidatorResult> ValidateAsync(CreateClientRequest instance, ValidationContext context, CancellationToken cancellationToken = default)
    {
        var result = new ValidatorResult();

        if (!context.HttpContext.Request.RouteValues.TryGetValue<Guid>("projectId", out var projectId))
        {
            return ValidatorResult.Problem("Route parameter 'projectId' is required.", StatusCodes.Status400BadRequest);
        }

        var project = await _projectStore.GetByIdAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return ValidatorResult.Problem($"Project '{projectId}' was not found.", StatusCodes.Status404NotFound);
        }

        if (instance.Scopes is { Count: > 0 })
        {
            foreach (var (dimension, value) in instance.Scopes)
            {
                var scope = await _scopeStore.GetByDimensionAsync(dimension, cancellationToken).ConfigureAwait(false);
                if (scope is null)
                {
                    result.AddError($"Scope dimension '{dimension}' was not found.", nameof(instance.Scopes));
                    continue;
                }

                if (!scope.AllowedValues.Contains(value))
                {
                    result.AddError($"Value '{value}' is not allowed for scope dimension '{dimension}'.", nameof(instance.Scopes));
                }
            }
        }

        return result.IsFailed ? result : ValidatorResult.Success;
    }
}