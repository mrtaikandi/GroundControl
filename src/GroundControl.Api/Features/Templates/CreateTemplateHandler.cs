using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Templates;

internal sealed class CreateTemplateHandler : IEndpointHandler
{
    private readonly ITemplateStore _store;
    private readonly AuditRecorder _audit;

    public CreateTemplateHandler(ITemplateStore store, AuditRecorder audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(string.Empty, async (
                CreateTemplateRequest request,
                [FromServices] CreateTemplateHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(request, cancellationToken))
            .WithContractValidation<CreateTemplateRequest>()
            .RequireAuthorization(Permissions.TemplatesWrite)
            .WithName(nameof(CreateTemplateHandler));
    }

    private async Task<IResult> HandleAsync(CreateTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var template = new Template
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            Description = request.Description,
            GroupId = request.GroupId,
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty,
        };

        await _store.CreateAsync(template, cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync("Template", template.Id, template.GroupId, "Created", cancellationToken: cancellationToken).ConfigureAwait(false);

        return TypedResults.Created($"/api/templates/{template.Id}", TemplateResponse.From(template));
    }
}