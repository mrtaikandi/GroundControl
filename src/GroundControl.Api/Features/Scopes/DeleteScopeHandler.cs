using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Scopes;

internal sealed class DeleteScopeHandler : IEndpointHandler
{
    private readonly IScopeStore _store;
    private readonly AuditRecorder _audit;

    public DeleteScopeHandler(IScopeStore store, AuditRecorder audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                [FromServices] DeleteScopeHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, httpContext, cancellationToken))
            .WithEndpointValidation<DeleteScopeValidator>()
            .RequireAuthorization(Permissions.ScopesWrite)
            .WithSummary("Delete a scope")
            .WithDescription("Deletes a scope if it is not referenced by any configuration entries. Requires an If-Match header.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .WithName(nameof(DeleteScopeHandler));
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

        await _audit.RecordAsync("Scope", id, null, "Deleted", cancellationToken: cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}