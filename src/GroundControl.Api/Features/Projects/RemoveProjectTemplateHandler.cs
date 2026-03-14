using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Projects;

internal sealed class RemoveProjectTemplateHandler : IEndpointHandler
{
    private readonly IProjectStore _store;

    public RemoveProjectTemplateHandler(IProjectStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/{id:guid}/templates/{templateId:guid}", async (
                Guid id,
                Guid templateId,
                HttpContext httpContext,
                [FromServices] RemoveProjectTemplateHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, templateId, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.ProjectsWrite)
            .WithName(nameof(RemoveProjectTemplateHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, Guid templateId, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var project = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return TypedResults.Problem(detail: $"Project '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (!project.TemplateIds.Remove(templateId))
        {
            httpContext.Response.Headers.ETag = EntityTagHeaders.Format(project.Version);
            return TypedResults.Ok(ProjectResponse.From(project));
        }

        project.UpdatedAt = DateTimeOffset.UtcNow;
        project.UpdatedBy = Guid.Empty;

        var updated = await _store.UpdateAsync(project, project.Version, cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(project.Version);
        return TypedResults.Ok(ProjectResponse.From(project));
    }
}