using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Templates;

internal sealed class CreateTemplateHandler : IEndpointHandler
{
    private readonly ITemplateStore _store;

    public CreateTemplateHandler(ITemplateStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
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

        return TypedResults.Created($"/api/templates/{template.Id}", TemplateResponse.From(template));
    }
}