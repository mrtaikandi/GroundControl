using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Variables;

internal sealed class CreateVariableValidator : IAsyncValidator<CreateVariableRequest>
{
    private readonly IGroupStore _groupStore;
    private readonly IProjectStore _projectStore;
    private readonly IScopeStore _scopeStore;

    public CreateVariableValidator(IGroupStore groupStore, IProjectStore projectStore, IScopeStore scopeStore)
    {
        _groupStore = groupStore ?? throw new ArgumentNullException(nameof(groupStore));
        _projectStore = projectStore ?? throw new ArgumentNullException(nameof(projectStore));
        _scopeStore = scopeStore ?? throw new ArgumentNullException(nameof(scopeStore));
    }

    public async Task<ValidatorResult> ValidateAsync(CreateVariableRequest instance, ValidationContext context, CancellationToken cancellationToken = default)
    {
        var result = await ValidateScopeOwnershipAsync(instance, cancellationToken);
        if (result.IsFailed)
        {
            return result;
        }

        return await ValidateScopedValuesAsync(instance, cancellationToken);
    }

    private async Task<ValidatorResult> ValidateScopeOwnershipAsync(CreateVariableRequest instance, CancellationToken cancellationToken)
    {
        var result = new ValidatorResult();

        if (instance.Scope == VariableScope.Global)
        {
            if (instance.ProjectId.HasValue)
            {
                result.AddError("Global variables cannot have a ProjectId.", nameof(instance.ProjectId));
            }

            if (instance.GroupId.HasValue)
            {
                var group = await _groupStore.GetByIdAsync(instance.GroupId.Value, cancellationToken).ConfigureAwait(false);
                if (group is null)
                {
                    result.AddError("The specified group does not exist.", nameof(instance.GroupId));
                }
            }
        }
        else if (instance.Scope == VariableScope.Project)
        {
            if (!instance.ProjectId.HasValue)
            {
                result.AddError("Project variables must have a ProjectId.", nameof(instance.ProjectId));
            }
            else
            {
                var project = await _projectStore.GetByIdAsync(instance.ProjectId.Value, cancellationToken).ConfigureAwait(false);
                if (project is null)
                {
                    result.AddError("The specified project does not exist.", nameof(instance.ProjectId));
                }
            }

            if (instance.GroupId.HasValue)
            {
                result.AddError("Project variables cannot have a GroupId.", nameof(instance.GroupId));
            }
        }

        return result;
    }

    private async Task<ValidatorResult> ValidateScopedValuesAsync(CreateVariableRequest instance, CancellationToken cancellationToken)
    {
        var result = new ValidatorResult();

        foreach (var scopedValue in instance.Values)
        {
            foreach (var (dimension, value) in scopedValue.Scopes)
            {
                var scope = await _scopeStore.GetByDimensionAsync(dimension, cancellationToken).ConfigureAwait(false);
                if (scope is null)
                {
                    result.AddError($"Scope dimension '{dimension}' does not exist.", nameof(instance.Values));
                }
                else if (!scope.AllowedValues.Contains(value))
                {
                    result.AddError($"Value '{value}' is not allowed for scope dimension '{dimension}'.", nameof(instance.Values));
                }
            }
        }

        return result;
    }
}