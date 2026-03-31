using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Templates;

internal sealed class DeleteTemplateValidator : IEndpointValidator
{
    private readonly ITemplateStore _store;

    public DeleteTemplateValidator(ITemplateStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<ValidatorResult> ValidateAsync(ValidationContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HttpContext.Request.RouteValues.TryGetValue<Guid>("id", out var id))
        {
            return ValidatorResult.Problem("Route parameter 'id' is required.", StatusCodes.Status400BadRequest);
        }

        var template = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            return ValidatorResult.Problem($"Template '{id}' was not found.", StatusCodes.Status404NotFound);
        }

        var ifMatchResult = EntityTagHeaders.ValidateIfMatch(context.HttpContext);
        if (ifMatchResult.IsFailed)
        {
            return ifMatchResult;
        }

        var isReferenced = await _store.IsReferencedByProjectsAsync(id, cancellationToken).ConfigureAwait(false);
        if (isReferenced)
        {
            return ValidatorResult.Problem(
                $"Template '{template.Name}' cannot be deleted because it is referenced by one or more projects.",
                StatusCodes.Status409Conflict);
        }

        return ValidatorResult.Success;
    }
}