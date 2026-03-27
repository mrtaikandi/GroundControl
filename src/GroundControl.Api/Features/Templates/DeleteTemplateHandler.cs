using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Audit;
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
    private readonly AuditRecorder _audit;

    public DeleteTemplateHandler(ITemplateStore store, IConfigEntryStore configEntryStore, AuditRecorder audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _configEntryStore = configEntryStore ?? throw new ArgumentNullException(nameof(configEntryStore));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
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

        var template = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        var deleted = await _store.DeleteAsync(id, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        await _configEntryStore.DeleteAllByOwnerAsync(id, ConfigEntryOwnerType.Template, cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync("Template", id, template?.GroupId, "Deleted", cancellationToken: cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}