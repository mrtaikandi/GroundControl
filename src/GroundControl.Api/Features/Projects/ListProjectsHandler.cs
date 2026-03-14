using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Projects;

internal sealed class ListProjectsHandler : IEndpointHandler
{
    private readonly IProjectStore _store;

    public ListProjectsHandler(IProjectStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(string.Empty, async (
                [AsParameters] ProjectListQuery query,
                [FromServices] ListProjectsHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(query, cancellationToken))
            .RequireAuthorization(Permissions.ProjectsRead)
            .WithName(nameof(ListProjectsHandler));
    }

    private async Task<IResult> HandleAsync(ProjectListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            var result = await _store.ListAsync(query, cancellationToken).ConfigureAwait(false);
            return TypedResults.Ok(new PaginatedResponse<ProjectResponse>
            {
                Data = result.Items.Select(ProjectResponse.From).ToList(),
                NextCursor = result.NextCursor,
                PreviousCursor = result.PreviousCursor,
                TotalCount = result.TotalCount,
            });
        }
        catch (ValidationException validationException)
        {
            return TypedResults.Problem(
                detail: validationException.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}