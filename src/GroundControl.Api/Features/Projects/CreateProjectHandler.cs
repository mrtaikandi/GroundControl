using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Projects;

internal sealed class CreateProjectHandler : IEndpointHandler
{
    private readonly IProjectStore _store;

    public CreateProjectHandler(IProjectStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(string.Empty, async (
                CreateProjectRequest request,
                [FromServices] CreateProjectHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(request, cancellationToken))
            .WithValidationOn<CreateProjectRequest>()
            .RequireAuthorization(Permissions.ProjectsWrite)
            .WithName(nameof(CreateProjectHandler));
    }

    private async Task<IResult> HandleAsync(CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var project = new Project
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            Description = request.Description,
            GroupId = request.GroupId,
            TemplateIds = request.TemplateIds is { Count: > 0 } ? [.. request.TemplateIds] : [],
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty,
        };

        try
        {
            await _store.CreateAsync(project, cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateKeyException)
        {
            return TypedResults.Problem(
                detail: $"A project with name '{request.Name}' already exists for this group.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return TypedResults.Created($"/api/projects/{project.Id}", ProjectResponse.From(project));
    }
}