using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Extensions.Http;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Clients;

internal sealed class DeleteClientValidator : IEndpointValidator
{
    private readonly IClientStore _store;

    public DeleteClientValidator(IClientStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<ValidatorResult> ValidateAsync(ValidationContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HttpContext.Request.RouteValues.TryGetValue<Guid>("projectId", out var projectId))
        {
            return ValidatorResult.Problem("Route parameter 'projectId' is required.", StatusCodes.Status400BadRequest);
        }

        if (!context.HttpContext.Request.RouteValues.TryGetValue<Guid>("id", out var id))
        {
            return ValidatorResult.Problem("Route parameter 'id' is required.", StatusCodes.Status400BadRequest);
        }

        var client = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (client is null || client.ProjectId != projectId)
        {
            return ValidatorResult.Problem($"Client '{id}' was not found.", StatusCodes.Status404NotFound);
        }

        var ifMatchResult = EntityTagHeaders.ValidateIfMatch(context.HttpContext);
        return ifMatchResult.IsFailed ? ifMatchResult : ValidatorResult.Success;
    }
}