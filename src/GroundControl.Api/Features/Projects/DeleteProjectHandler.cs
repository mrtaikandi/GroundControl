using GroundControl.Api.Shared;
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

    public DeleteProjectHandler(IProjectStore store, IConfigEntryStore configEntryStore, IClientStore clientStore)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _configEntryStore = configEntryStore ?? throw new ArgumentNullException(nameof(configEntryStore));
        _clientStore = clientStore ?? throw new ArgumentNullException(nameof(clientStore));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                [FromServices] DeleteProjectHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.ProjectsWrite)
            .WithName(nameof(DeleteProjectHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var project = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return TypedResults.Problem(detail: $"Project '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion))
        {
            return TypedResults.Problem(detail: "If-Match header is required.", statusCode: StatusCodes.Status428PreconditionRequired);
        }

        await _configEntryStore.DeleteAllByOwnerAsync(id, ConfigEntryOwnerType.Project, cancellationToken).ConfigureAwait(false);
        await _clientStore.DeleteByProjectAsync(id, cancellationToken).ConfigureAwait(false);

        var deleted = await _store.DeleteAsync(id, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        return TypedResults.NoContent();
    }
}