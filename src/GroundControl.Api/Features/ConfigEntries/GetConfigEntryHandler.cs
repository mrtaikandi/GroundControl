using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.ConfigEntries;

internal sealed class GetConfigEntryHandler : IEndpointHandler
{
    private readonly IConfigEntryStore _store;
    private readonly SensitiveValueMasker _masker;

    public GetConfigEntryHandler(IConfigEntryStore store, SensitiveValueMasker masker)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _masker = masker ?? throw new ArgumentNullException(nameof(masker));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                [FromQuery] bool? decrypt,
                [FromServices] GetConfigEntryHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, decrypt == true, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.ConfigEntriesRead)
            .WithSummary("Get a configuration entry")
            .WithDescription("Returns a configuration entry by its unique identifier. Optionally decrypts sensitive values. Includes an ETag header.")
            .Produces<ConfigEntryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName(nameof(GetConfigEntryHandler));
    }

    private async Task<IResult> HandleAsync(
        Guid id,
        bool decrypt,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var entry = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return TypedResults.Problem(detail: $"Config entry '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        var canDecrypt = SensitiveValueMasker.CanDecrypt(httpContext, decrypt);
        var maskedValues = await _masker.MaskOrDecryptAsync(
            entry.Values, entry.IsSensitive, canDecrypt, "ConfigEntry", entry.Id, null, cancellationToken).ConfigureAwait(false);

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(entry.Version);
        return TypedResults.Ok(ConfigEntryResponse.From(entry, maskedValues));
    }
}