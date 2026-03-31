using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Projects;

internal sealed class CreateProjectValidator : IAsyncValidator<CreateProjectRequest>
{
    private readonly IGroupStore _groupStore;
    private readonly ITemplateStore _templateStore;

    public CreateProjectValidator(IGroupStore groupStore, ITemplateStore templateStore)
    {
        _groupStore = groupStore ?? throw new ArgumentNullException(nameof(groupStore));
        _templateStore = templateStore ?? throw new ArgumentNullException(nameof(templateStore));
    }

    public async Task<ValidatorResult> ValidateAsync(CreateProjectRequest instance, ValidationContext context, CancellationToken cancellationToken = default)
    {
        var result = new ValidatorResult();

        if (instance.GroupId.HasValue)
        {
            var group = await _groupStore.GetByIdAsync(instance.GroupId.Value, cancellationToken).ConfigureAwait(false);
            if (group is null)
            {
                result.AddError($"Group '{instance.GroupId.Value}' was not found.", nameof(instance.GroupId));
            }
        }

        if (instance.TemplateIds is { Count: > 0 })
        {
            foreach (var templateId in instance.TemplateIds)
            {
                var template = await _templateStore.GetByIdAsync(templateId, cancellationToken).ConfigureAwait(false);
                if (template is null)
                {
                    result.AddError($"Template '{templateId}' was not found.", nameof(instance.TemplateIds));
                }
            }
        }

        return result.IsFailed ? result : ValidatorResult.Success;
    }
}