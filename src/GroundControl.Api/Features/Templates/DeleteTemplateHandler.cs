using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Templates;

internal sealed class DeleteTemplateHandler : IEndpointHandler
{
    private readonly IConfigEntryStore _configEntryStore;
    private readonly ITemplateStore _store;

    public DeleteTemplateHandler(ITemplateStore store, IConfigEntryStore configEntryStore)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _configEntryStore = configEntryStore ?? throw new ArgumentNullException(nameof(configEntryStore));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                [FromServices] DeleteTemplateHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.TemplatesWrite)
            .WithName(nameof(DeleteTemplateHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var template = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            return TypedResults.Problem(detail: $"Template '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion))
        {
            return TypedResults.Problem(detail: "If-Match header is required.", statusCode: StatusCodes.Status428PreconditionRequired);
        }

        var isReferenced = await _store.IsReferencedByProjectsAsync(id, cancellationToken).ConfigureAwait(false);
        if (isReferenced)
        {
            return TypedResults.Problem(
                detail: $"Template '{template.Name}' cannot be deleted because it is referenced by one or more projects.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var deleted = await _store.DeleteAsync(id, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        await _configEntryStore.DeleteAllByOwnerAsync(id, ConfigEntryOwnerType.Template, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}