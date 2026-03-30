using AspNetCore.Identity.MongoDbCore.Models;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Shared.Security.Authentication;

internal sealed partial class AdminSeedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AuthenticationOptions _options;
    private readonly ILogger<AdminSeedService> _logger;

    public AdminSeedService(ILogger<AdminSeedService> logger, IServiceProvider serviceProvider, IOptions<AuthenticationOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var seedOptions = _options.Seed;
        if (string.IsNullOrWhiteSpace(seedOptions.AdminPassword))
        {
            LogNoSeedPassword(_logger);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<MongoIdentityUser<Guid>>>();
        var userStore = scope.ServiceProvider.GetRequiredService<IUserStore>();
        var roleStore = scope.ServiceProvider.GetRequiredService<IRoleStore>();

        var email = seedOptions.AdminEmail;
        var username = seedOptions.AdminUsername;

        // Idempotent: skip if domain user already exists
        var existingUser = await userStore.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (existingUser is not null)
        {
            LogAdminAlreadyExists(_logger, email);
            return;
        }

        // Look up the Admin role (created by RoleSeedService)
        var adminRole = await roleStore.GetByNameAsync("Admin", cancellationToken).ConfigureAwait(false);
        if (adminRole is null)
        {
            LogAdminRoleNotFound(_logger);
            return;
        }

        // Create identity user
        var userId = Guid.CreateVersion7();
        var identityUser = new MongoIdentityUser<Guid>
        {
            Id = userId,
            Email = email,
            UserName = username,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = username.ToUpperInvariant()
        };

        var result = await userManager.CreateAsync(identityUser, seedOptions.AdminPassword).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            LogIdentityUserCreationFailed(_logger, errors);
            return;
        }

        // Create corresponding domain user with system-wide Admin grant
        var timestamp = DateTimeOffset.UtcNow;
        var domainUser = new User
        {
            Id = userId,
            Username = username,
            Email = email,
            IsActive = true,
            Grants = [new Grant { Resource = null, RoleId = adminRole.Id }],
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty,
        };

        await userStore.CreateAsync(domainUser, cancellationToken).ConfigureAwait(false);
        LogAdminSeeded(_logger, email);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(1, LogLevel.Warning, "Admin seed password not configured. Skipping admin seed. Set 'Authentication__Seed__AdminPassword' to enable.")]
    private static partial void LogNoSeedPassword(ILogger<AdminSeedService> logger);

    [LoggerMessage(2, LogLevel.Debug, "Admin user with email '{Email}' already exists. Skipping seed.")]
    private static partial void LogAdminAlreadyExists(ILogger<AdminSeedService> logger, string email);

    [LoggerMessage(3, LogLevel.Error, "Admin role not found. Ensure RoleSeedService runs before AdminSeedService.")]
    private static partial void LogAdminRoleNotFound(ILogger<AdminSeedService> logger);

    [LoggerMessage(4, LogLevel.Error, "Failed to create identity user for admin seed: {Errors}")]
    private static partial void LogIdentityUserCreationFailed(ILogger<AdminSeedService> logger, string errors);

    [LoggerMessage(5, LogLevel.Information, "Seed admin user created with email '{Email}'. Change the password immediately.")]
    private static partial void LogAdminSeeded(ILogger<AdminSeedService> logger, string email);
}