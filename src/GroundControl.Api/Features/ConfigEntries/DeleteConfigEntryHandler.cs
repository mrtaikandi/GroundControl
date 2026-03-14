using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.ConfigEntries;

internal sealed class DeleteConfigEntryHandler : IEndpointHandler
{
    private readonly IConfigEntryStore _store;

    public DeleteConfigEntryHandler(IConfigEntryStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                [FromServices] DeleteConfigEntryHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, httpContext, cancellationToken))
            .WithEndpointValidation<DeleteConfigEntryValidator>()
            .RequireAuthorization(Permissions.ConfigEntriesWrite)
            .WithName(nameof(DeleteConfigEntryHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion, out var problem))
        {
            return problem;
        }

        var deleted = await _store.DeleteAsync(id, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        return TypedResults.NoContent();
    }
}