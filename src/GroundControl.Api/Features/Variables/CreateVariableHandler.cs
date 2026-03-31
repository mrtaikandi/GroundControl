using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Variables;

internal sealed class CreateVariableHandler : IEndpointHandler
{
    private readonly IVariableStore _store;
    private readonly AuditRecorder _audit;

    public CreateVariableHandler(IVariableStore store, AuditRecorder audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(string.Empty, async (
                CreateVariableRequest request,
                [FromServices] CreateVariableHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(request, cancellationToken))
            .WithContractValidation<CreateVariableRequest>()
            .RequireAuthorization(Permissions.VariablesWrite)
            .WithName(nameof(CreateVariableHandler));
    }

    private async Task<IResult> HandleAsync(CreateVariableRequest request, CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var variable = new Variable
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            Description = request.Description,
            Scope = request.Scope,
            GroupId = request.GroupId,
            ProjectId = request.ProjectId,
            Values = [.. request.Values.Select(v => new ScopedValue(v.Value, v.Scopes))],
            IsSensitive = request.IsSensitive,
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty,
        };

        try
        {
            await _store.CreateAsync(variable, cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateKeyException)
        {
            return TypedResults.Problem(
                detail: $"A variable with name '{request.Name}' already exists for this owner.",
                statusCode: StatusCodes.Status409Conflict);
        }

        await _audit.RecordAsync("Variable", variable.Id, variable.GroupId, "Created", cancellationToken: cancellationToken).ConfigureAwait(false);

        return TypedResults.Created($"/api/variables/{variable.Id}", VariableResponse.From(variable));
    }
}