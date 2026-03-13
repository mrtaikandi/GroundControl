using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using ValidationContext = GroundControl.Api.Shared.Validation.ValidationContext;

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
        var errors = await ValidateScopeOwnershipAsync(instance, cancellationToken);
        if (errors.Count > 0)
        {
            return ValidatorResult.ValidationProblem(errors);
        }

        errors = await ValidateScopedValuesAsync(instance, cancellationToken);
        return errors.Count > 0 ? ValidatorResult.ValidationProblem(errors) : ValidatorResult.Success;
    }

    private async Task<List<ValidationResult>> ValidateScopeOwnershipAsync(CreateVariableRequest instance, CancellationToken cancellationToken)
    {
        var errors = new List<ValidationResult>();

        if (instance.Scope == VariableScope.Global)
        {
            if (instance.ProjectId.HasValue)
            {
                errors.Add(ValidationResult.Error("Global variables cannot have a ProjectId.", [nameof(instance.ProjectId)]));
            }

            if (instance.GroupId.HasValue)
            {
                var group = await _groupStore.GetByIdAsync(instance.GroupId.Value, cancellationToken).ConfigureAwait(false);
                if (group is null)
                {
                    errors.Add(ValidationResult.Error("The specified group does not exist.", [nameof(instance.GroupId)]));
                }
            }
        }
        else if (instance.Scope == VariableScope.Project)
        {
            if (!instance.ProjectId.HasValue)
            {
                errors.Add(ValidationResult.Error("Project variables must have a ProjectId.", [nameof(instance.ProjectId)]));
            }
            else
            {
                var project = await _projectStore.GetByIdAsync(instance.ProjectId.Value, cancellationToken).ConfigureAwait(false);
                if (project is null)
                {
                    errors.Add(ValidationResult.Error("The specified project does not exist.", [nameof(instance.ProjectId)]));
                }
            }

            if (instance.GroupId.HasValue)
            {
                errors.Add(ValidationResult.Error("Project variables cannot have a GroupId.", [nameof(instance.GroupId)]));
            }
        }

        return errors;
    }

    private async Task<List<ValidationResult>> ValidateScopedValuesAsync(CreateVariableRequest instance, CancellationToken cancellationToken)
    {
        var errors = new List<ValidationResult>();

        foreach (var scopedValue in instance.Values)
        {
            foreach (var (dimension, value) in scopedValue.Scopes)
            {
                var scope = await _scopeStore.GetByDimensionAsync(dimension, cancellationToken).ConfigureAwait(false);
                if (scope is null)
                {
                    errors.Add(ValidationResult.Error($"Scope dimension '{dimension}' does not exist.", [nameof(instance.Values)]));
                }
                else if (!scope.AllowedValues.Contains(value))
                {
                    errors.Add(ValidationResult.Error($"Value '{value}' is not allowed for scope dimension '{dimension}'.", [nameof(instance.Values)]));
                }
            }
        }

        return errors;
    }
}