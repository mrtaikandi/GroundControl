using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Projects;

internal sealed class UpdateProjectHandler : IEndpointHandler
{
    private readonly IProjectStore _store;
    private readonly AuditRecorder _audit;

    public UpdateProjectHandler(IProjectStore store, AuditRecorder audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}", async (
                Guid id,
                UpdateProjectRequest request,
                HttpContext httpContext,
                [FromServices] UpdateProjectHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, request, httpContext, cancellationToken))
            .WithContractValidation<UpdateProjectRequest>()
            .RequireAuthorization(Permissions.ProjectsWrite)
            .WithName(nameof(UpdateProjectHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, UpdateProjectRequest request, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(httpContext);

        var project = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return TypedResults.Problem(detail: $"Project '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion, out var problem))
        {
            return problem;
        }

        var oldName = project.Name;
        var oldDescription = project.Description;
        var oldGroupId = project.GroupId;
        var oldTemplateIds = project.TemplateIds.ToList();

        project.Name = request.Name;
        project.Description = request.Description;
        project.GroupId = request.GroupId;
        project.TemplateIds.Clear();
        if (request.TemplateIds is { Count: > 0 })
        {
            foreach (var templateId in request.TemplateIds)
            {
                project.TemplateIds.Add(templateId);
            }
        }

        project.UpdatedAt = DateTimeOffset.UtcNow;
        project.UpdatedBy = Guid.Empty;

        var updated = await _store.UpdateAsync(project, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        List<FieldChange> changes = [
            .. AuditRecorder.CompareFields("Name", oldName, project.Name),
            .. AuditRecorder.CompareFields("Description", oldDescription, project.Description),
            .. AuditRecorder.CompareFields("GroupId", oldGroupId, project.GroupId),
            .. AuditRecorder.CompareCollections("TemplateIds", oldTemplateIds, project.TemplateIds.ToList()),
        ];

        await _audit.RecordAsync("Project", project.Id, project.GroupId, "Updated", changes, cancellationToken: cancellationToken).ConfigureAwait(false);

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(project.Version);
        return TypedResults.Ok(ProjectResponse.From(project));
    }
}