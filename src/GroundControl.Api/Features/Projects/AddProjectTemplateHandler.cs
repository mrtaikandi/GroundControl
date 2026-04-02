using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Projects;

internal sealed class AddProjectTemplateHandler : IEndpointHandler
{
    private readonly IProjectStore _store;
    private readonly AuditRecorder _audit;

    public AddProjectTemplateHandler(IProjectStore store, AuditRecorder audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
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
            .WithSummary("Add a template to a project")
            .WithDescription("Associates a template with the specified project. Requires an If-Match header.")
            .Produces<ProjectResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
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

        var metadata = new Dictionary<string, string> { ["TemplateId"] = templateId.ToString() };
        await _audit.RecordAsync("Project", id, project.GroupId, "TemplateAdded", metadata: metadata, cancellationToken: cancellationToken).ConfigureAwait(false);

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(project.Version);
        return TypedResults.Ok(ProjectResponse.From(project));
    }
}