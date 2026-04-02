using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Projects;

internal sealed class DeleteProjectHandler : IEndpointHandler
{
    private readonly IProjectStore _store;
    private readonly IConfigEntryStore _configEntryStore;
    private readonly IClientStore _clientStore;
    private readonly AuditRecorder _audit;

    public DeleteProjectHandler(IProjectStore store, IConfigEntryStore configEntryStore, IClientStore clientStore, AuditRecorder audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _configEntryStore = configEntryStore ?? throw new ArgumentNullException(nameof(configEntryStore));
        _clientStore = clientStore ?? throw new ArgumentNullException(nameof(clientStore));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                [FromServices] DeleteProjectHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, httpContext, cancellationToken))
            .WithEndpointValidation<DeleteProjectValidator>()
            .RequireAuthorization(Permissions.ProjectsWrite)
            .WithSummary("Delete a project")
            .WithDescription("Deletes a project if it has no dependents. Requires an If-Match header.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .WithName(nameof(DeleteProjectHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion, out var problem))
        {
            return problem;
        }

        var project = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        await _configEntryStore.DeleteAllByOwnerAsync(id, ConfigEntryOwnerType.Project, cancellationToken).ConfigureAwait(false);
        await _clientStore.DeleteByProjectAsync(id, cancellationToken).ConfigureAwait(false);

        var deleted = await _store.DeleteAsync(id, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        await _audit.RecordAsync("Project", id, project?.GroupId, "Deleted", cancellationToken: cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}