using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Projects;

internal sealed class DeleteProjectValidator : IEndpointValidator
{
    private readonly IProjectStore _store;

    public DeleteProjectValidator(IProjectStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<ValidatorResult> ValidateAsync(ValidationContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HttpContext.Request.RouteValues.TryGetValue<Guid>("id", out var id))
        {
            return ValidatorResult.Problem("Route parameter 'id' is required.", StatusCodes.Status400BadRequest);
        }

        var project = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return ValidatorResult.Problem($"Project '{id}' was not found.", StatusCodes.Status404NotFound);
        }

        var ifMatchResult = EntityTagHeaders.ValidateIfMatch(context.HttpContext);
        if (ifMatchResult.IsFailed)
        {
            return ifMatchResult;
        }

        return ValidatorResult.Success;
    }
}