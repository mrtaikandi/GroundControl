using GroundControl.Api.Shared.Extensions.Http;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Projects;

internal sealed class AddProjectTemplateValidator : IEndpointValidator
{
    private readonly ITemplateStore _templateStore;

    public AddProjectTemplateValidator(ITemplateStore templateStore)
    {
        _templateStore = templateStore ?? throw new ArgumentNullException(nameof(templateStore));
    }

    public async Task<ValidatorResult> ValidateAsync(ValidationContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HttpContext.Request.RouteValues.TryGetValue<Guid>("templateId", out var templateId))
        {
            return ValidatorResult.Problem("Route parameter 'templateId' is required.", StatusCodes.Status400BadRequest);
        }

        var template = await _templateStore.GetByIdAsync(templateId, cancellationToken).ConfigureAwait(false);
        return template is null
            ? ValidatorResult.Problem($"Template '{templateId}' was not found.", StatusCodes.Status404NotFound)
            : ValidatorResult.Success;
    }
}