using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Extensions.Http;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Variables;

internal sealed class DeleteVariableValidator : IEndpointValidator
{
    private readonly IVariableStore _store;

    public DeleteVariableValidator(IVariableStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<ValidatorResult> ValidateAsync(ValidationContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HttpContext.Request.RouteValues.TryGetValue<Guid>("id", out var id))
        {
            return ValidatorResult.Problem("Route parameter 'id' is required.", StatusCodes.Status400BadRequest);
        }

        var variable = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (variable is null)
        {
            return ValidatorResult.Problem($"Variable '{id}' was not found.", StatusCodes.Status404NotFound);
        }

        if (!EntityTagHeaders.TryParseIfMatch(context.HttpContext, out _))
        {
            return ValidatorResult.Problem("If-Match header is required.", StatusCodes.Status428PreconditionRequired);
        }

        return ValidatorResult.Success;
    }
}