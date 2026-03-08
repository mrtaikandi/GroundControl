using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Templates;

internal sealed class UpdateTemplateHandler : IEndpointHandler
{
    private readonly IGroupStore _groupStore;
    private readonly ITemplateStore _store;

    public UpdateTemplateHandler(ITemplateStore store, IGroupStore groupStore)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _groupStore = groupStore ?? throw new ArgumentNullException(nameof(groupStore));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}", async (
                Guid id,
                UpdateTemplateRequest request,
                HttpContext httpContext,
                [FromServices] UpdateTemplateHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, request, httpContext, cancellationToken))
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

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion))
        {
            return TypedResults.Problem(detail: "If-Match header is required.", statusCode: StatusCodes.Status428PreconditionRequired);
        }

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

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(template.Version);
        return TypedResults.Ok(TemplateResponse.From(template));
    }
}