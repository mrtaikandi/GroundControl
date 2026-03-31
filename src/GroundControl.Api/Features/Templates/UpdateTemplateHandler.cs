using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Templates;

internal sealed class UpdateTemplateHandler : IEndpointHandler
{
    private readonly ITemplateStore _store;
    private readonly AuditRecorder _audit;

    public UpdateTemplateHandler(ITemplateStore store, AuditRecorder audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}", async (
                Guid id,
                UpdateTemplateRequest request,
                HttpContext httpContext,
                [FromServices] UpdateTemplateHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, request, httpContext, cancellationToken))
            .WithContractValidation<UpdateTemplateRequest>()
            .RequireAuthorization(Permissions.TemplatesWrite)
            .WithName(nameof(UpdateTemplateHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, UpdateTemplateRequest request, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(httpContext);

        var template = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            return TypedResults.Problem(detail: $"Template '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion, out var problem))
        {
            return problem;
        }

        var oldName = template.Name;
        var oldDescription = template.Description;
        var oldGroupId = template.GroupId;

        template.Name = request.Name;
        template.Description = request.Description;
        template.GroupId = request.GroupId;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        template.UpdatedBy = Guid.Empty;

        var updated = await _store.UpdateAsync(template, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        List<FieldChange> changes = [
            .. AuditRecorder.CompareFields("Name", oldName, template.Name),
            .. AuditRecorder.CompareFields("Description", oldDescription, template.Description),
            .. AuditRecorder.CompareFields("GroupId", oldGroupId, template.GroupId),
        ];

        await _audit.RecordAsync("Template", template.Id, template.GroupId, "Updated", changes, cancellationToken: cancellationToken).ConfigureAwait(false);

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(template.Version);
        return TypedResults.Ok(TemplateResponse.From(template));
    }
}