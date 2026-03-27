using System.Buffers.Text;
using System.Security.Cryptography;
using GroundControl.Api.Features.Clients.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Clients;

internal sealed class CreateClientHandler : IEndpointHandler
{
    private readonly IClientStore _store;
    private readonly IValueProtector _protector;
    private readonly AuditRecorder _audit;

    public CreateClientHandler(IClientStore store, IValueProtector protector, AuditRecorder audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(string.Empty, async (
                Guid projectId,
                CreateClientRequest request,
                [FromServices] CreateClientHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(projectId, request, cancellationToken))
            .WithContractValidation<CreateClientRequest>()
            .RequireAuthorization(Permissions.ClientsWrite)
            .WithName(nameof(CreateClientHandler));
    }

    private async Task<IResult> HandleAsync(Guid projectId, CreateClientRequest request, CancellationToken cancellationToken = default)
    {
        var rawSecretBytes = RandomNumberGenerator.GetBytes(32);
        var rawSecret = Base64Url.EncodeToString(rawSecretBytes);
        var encryptedSecret = _protector.Protect(rawSecret);

        var timestamp = DateTimeOffset.UtcNow;
        var client = new Client
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            Name = request.Name,
            Scopes = request.Scopes is { Count: > 0 } ? new Dictionary<string, string>(request.Scopes) : [],
            Secret = encryptedSecret,
            IsActive = true,
            ExpiresAt = request.ExpiresAt,
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty,
        };

        await _store.CreateAsync(client, cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync("Client", client.Id, null, "Created", cancellationToken: cancellationToken).ConfigureAwait(false);

        return TypedResults.Created(
            $"/api/projects/{projectId}/clients/{client.Id}",
            CreateClientResponse.From(client, rawSecret));
    }
}