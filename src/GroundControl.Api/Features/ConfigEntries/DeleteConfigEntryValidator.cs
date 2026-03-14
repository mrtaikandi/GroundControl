using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Extensions.Http;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.ConfigEntries;

internal sealed class DeleteConfigEntryValidator : IEndpointValidator
{
    private readonly IConfigEntryStore _store;

    public DeleteConfigEntryValidator(IConfigEntryStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<ValidatorResult> ValidateAsync(ValidationContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HttpContext.Request.RouteValues.TryGetValue<Guid>("id", out var id))
        {
            return ValidatorResult.Problem("Route parameter 'id' is required.", StatusCodes.Status400BadRequest);
        }

        var entry = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return ValidatorResult.Problem($"Config entry '{id}' was not found.", StatusCodes.Status404NotFound);
        }

        if (!EntityTagHeaders.TryParseIfMatch(context.HttpContext, out _))
        {
            return ValidatorResult.Problem("If-Match header is required.", StatusCodes.Status428PreconditionRequired);
        }

        return ValidatorResult.Success;
    }
}