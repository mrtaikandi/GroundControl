using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Templates;

internal sealed class CreateTemplateHandler : IEndpointHandler
{
    private readonly IGroupStore _groupStore;
    private readonly ITemplateStore _store;

    public CreateTemplateHandler(ITemplateStore store, IGroupStore groupStore)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _groupStore = groupStore ?? throw new ArgumentNullException(nameof(groupStore));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(string.Empty, async (
                CreateTemplateRequest request,
                [FromServices] CreateTemplateHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(request, cancellationToken))
            .RequireAuthorization(Permissions.TemplatesWrite)
            .WithName(nameof(CreateTemplateHandler));
    }

    private async Task<IResult> HandleAsync(CreateTemplateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.GroupId.HasValue)
        {
            var group = await _groupStore.GetByIdAsync(request.GroupId.Value, cancellationToken).ConfigureAwait(false);
            if (group is null)
            {
                return TypedResults.Problem(
                    detail: $"Group '{request.GroupId.Value}' was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }
        }

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