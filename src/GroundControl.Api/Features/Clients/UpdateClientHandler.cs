using GroundControl.Api.Features.Clients.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Clients;

internal sealed class UpdateClientHandler : IEndpointHandler
{
    private readonly IClientStore _store;
    private readonly AuditRecorder _audit;

    public UpdateClientHandler(IClientStore store, AuditRecorder audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}", async (
                Guid projectId,
                Guid id,
                UpdateClientRequest request,
                HttpContext httpContext,
                [FromServices] UpdateClientHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(projectId, id, request, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.ClientsWrite)
            .WithName(nameof(UpdateClientHandler));
    }

    private async Task<IResult> HandleAsync(Guid projectId, Guid id, UpdateClientRequest request, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(httpContext);

        var client = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (client is null || client.ProjectId != projectId)
        {
            return TypedResults.Problem(detail: $"Client '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion, out var problem))
        {
            return problem;
        }

        var oldName = client.Name;
        var oldIsActive = client.IsActive;
        var oldExpiresAt = client.ExpiresAt;

        client.Name = request.Name;
        client.IsActive = request.IsActive;
        client.ExpiresAt = request.ExpiresAt;
        client.UpdatedAt = DateTimeOffset.UtcNow;
        client.UpdatedBy = Guid.Empty;

        var updated = await _store.UpdateAsync(client, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        List<FieldChange> changes = [
            .. AuditRecorder.CompareFields("Name", oldName, client.Name),
            .. AuditRecorder.CompareFields("IsActive", oldIsActive.ToString(), client.IsActive.ToString()),
            .. AuditRecorder.CompareFields("ExpiresAt", oldExpiresAt?.ToString("O"), client.ExpiresAt?.ToString("O")),
        ];

        await _audit.RecordAsync("Client", client.Id, null, "Updated", changes, cancellationToken: cancellationToken).ConfigureAwait(false);

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(client.Version);
        return TypedResults.Ok(ClientResponse.From(client));
    }
}