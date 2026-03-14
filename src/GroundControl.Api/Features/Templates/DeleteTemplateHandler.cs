using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Validation;
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
            .WithEndpointValidation<DeleteTemplateValidator>()
            .RequireAuthorization(Permissions.TemplatesWrite)
            .WithName(nameof(DeleteTemplateHandler));
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

        await _configEntryStore.DeleteAllByOwnerAsync(id, ConfigEntryOwnerType.Template, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}