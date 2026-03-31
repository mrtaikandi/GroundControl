using AspNetCore.Identity.MongoDbCore.Models;
using GroundControl.Api.Core.Authentication;
using GroundControl.Api.Features.Users.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Features.Users;

internal sealed class CreateUserHandler : IEndpointHandler
{
    private readonly IUserStore _userStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly AuditRecorder _audit;
    private readonly AuthenticationOptions _options;

    public CreateUserHandler(IUserStore userStore, IServiceProvider serviceProvider, AuditRecorder audit, IOptions<AuthenticationOptions> options)
    {
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _options = options.Value;
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(string.Empty, async (
                CreateUserRequest request,
                [FromServices] CreateUserHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(request, cancellationToken))
            .WithContractValidation<CreateUserRequest>()
            .RequireAuthorization(Permissions.UsersWrite)
            .WithName(nameof(CreateUserHandler));
    }

    private async Task<IResult> HandleAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var userId = Guid.CreateVersion7();

        // In BuiltIn mode, create the identity user first
        if (_options.Mode is AuthenticationMode.BuiltIn)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return TypedResults.Problem(
                    detail: "Password is required when using BuiltIn authentication mode.",
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var userManager = _serviceProvider.GetRequiredService<UserManager<MongoIdentityUser<Guid>>>();
            var identityUser = new MongoIdentityUser<Guid>
            {
                Id = userId,
                Email = request.Email,
                UserName = request.Username,
                NormalizedEmail = request.Email.ToUpperInvariant(),
                NormalizedUserName = request.Username.ToUpperInvariant()
            };

            var identityResult = await userManager.CreateAsync(identityUser, request.Password).ConfigureAwait(false);
            if (!identityResult.Succeeded)
            {
                var errors = string.Join("; ", identityResult.Errors.Select(e => e.Description));
                return TypedResults.Problem(
                    detail: errors,
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }
        }

        var timestamp = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = userId,
            Username = request.Username,
            Email = request.Email,
            IsActive = true,
            Grants = request.Grants?.Select(g => g.ToEntity()).ToList() ?? [],
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty,
        };

        await _userStore.CreateAsync(user, cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync("User", user.Id, null, "Created", cancellationToken: cancellationToken).ConfigureAwait(false);

        return TypedResults.Created($"/api/users/{user.Id}", UserResponse.From(user));
    }
}