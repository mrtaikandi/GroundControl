using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Projects;

internal sealed class AddProjectTemplateHandler : IEndpointHandler
{
    private readonly IProjectStore _store;

    public AddProjectTemplateHandler(IProjectStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}/templates/{templateId:guid}", async (
                Guid id,
                Guid templateId,
                HttpContext httpContext,
                [FromServices] AddProjectTemplateHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, templateId, httpContext, cancellationToken))
            .WithEndpointValidation<AddProjectTemplateValidator>()
            .RequireAuthorization(Permissions.ProjectsWrite)
            .WithName(nameof(AddProjectTemplateHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, Guid templateId, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var project = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return TypedResults.Problem(detail: $"Project '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (project.TemplateIds.Contains(templateId))
        {
            httpContext.Response.Headers.ETag = EntityTagHeaders.Format(project.Version);
            return TypedResults.Ok(ProjectResponse.From(project));
        }

        project.TemplateIds.Add(templateId);
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