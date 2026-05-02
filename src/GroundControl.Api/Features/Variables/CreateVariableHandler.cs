using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Variables;

internal sealed class CreateVariableHandler : IEndpointHandler
{
    private readonly IVariableStore _store;
    private readonly AuditRecorder _audit;
    private readonly SensitiveSourceValueProtector _protector;

    public CreateVariableHandler(IVariableStore store, AuditRecorder audit, SensitiveSourceValueProtector protector)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(string.Empty, async (
                CreateVariableRequest request,
                [FromServices] CreateVariableHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(request, cancellationToken))
            .WithContractValidation<CreateVariableRequest>()
            .RequireAuthorization(Permissions.VariablesWrite)
            .WithSummary("Create a variable")
            .WithDescription("Creates a new variable for use in template interpolation.")
            .Produces<VariableResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(CreateVariableHandler));
    }

    private async Task<IResult> HandleAsync(CreateVariableRequest request, CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var plaintextValues = request.Values.Select(v => new ScopedValue(v.Value, v.Scopes));
        var protectedValues = _protector.ProtectValues(plaintextValues, request.IsSensitive);

        var variable = new Variable
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            Description = request.Description,
            Scope = request.Scope,
            GroupId = request.GroupId,
            ProjectId = request.ProjectId,
            Values = [.. protectedValues],
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

        var responseValues = _protector.MaskValues(variable.Values, variable.IsSensitive);
        return TypedResults.Created($"/api/variables/{variable.Id}", VariableResponse.From(variable, [.. responseValues]));
    }
}