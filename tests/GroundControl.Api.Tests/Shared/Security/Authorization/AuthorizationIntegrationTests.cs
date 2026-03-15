using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Shared.Security.Authorization;

/// <summary>
/// Integration tests that verify authorization behavior through the full HTTP pipeline.
/// Uses a test authentication handler to simulate authenticated users with specific grants.
/// </summary>
[Collection("MongoDB")]
public sealed class AuthorizationIntegrationTests : ApiHandlerTestBase
{
    private const string TestUserIdHeader = "X-Test-User-Id";
    private static readonly Guid ViewerRoleId = Guid.CreateVersion7();
    private static readonly Guid EditorRoleId = Guid.CreateVersion7();

    private static readonly Role ViewerRole = new()
    {
        Id = ViewerRoleId,
        Name = "IntegrationViewer",
        Permissions =
        [
            Permissions.ScopesRead, Permissions.GroupsRead, Permissions.ProjectsRead,
            Permissions.TemplatesRead, Permissions.VariablesRead, Permissions.ConfigEntriesRead,
            Permissions.SnapshotsRead, Permissions.AuditRead
        ],
        Version = 1,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static readonly Role EditorRole = new()
    {
        Id = EditorRoleId,
        Name = "IntegrationEditor",
        Permissions =
        [
            Permissions.ScopesRead, Permissions.GroupsRead, Permissions.ProjectsRead, Permissions.ProjectsWrite,
            Permissions.TemplatesRead, Permissions.TemplatesWrite, Permissions.VariablesRead, Permissions.VariablesWrite,
            Permissions.ConfigEntriesRead, Permissions.ConfigEntriesWrite, Permissions.SnapshotsRead,
            Permissions.ClientsRead, Permissions.ClientsWrite, Permissions.AuditRead,
            Permissions.ScopesWrite
        ],
        Version = 1,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    public AuthorizationIntegrationTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task Viewer_GetScopes_Returns200()
    {
        // Arrange
        await using var factory = CreateAuthTestFactory();
        var (userId, _) = await SeedUserAsync(factory, ViewerRoleId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestUserIdHeader, userId.ToString());

        // Act
        var response = await client.GetAsync("/api/scopes", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Viewer_PostScopes_Returns403()
    {
        // Arrange
        await using var factory = CreateAuthTestFactory();
        var (userId, _) = await SeedUserAsync(factory, ViewerRoleId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestUserIdHeader, userId.ToString());

        var request = new { dimension = "test-dim", allowedValues = new[] { "A", "B" } };

        // Act
        var response = await client.PostAsJsonAsync("/api/scopes", request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Editor_PostScopes_Returns201()
    {
        // Arrange
        await using var factory = CreateAuthTestFactory();
        var (userId, _) = await SeedUserAsync(factory, EditorRoleId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestUserIdHeader, userId.ToString());

        var request = new { dimension = "test-dimension", allowedValues = new[] { "A", "B" } };

        // Act
        var response = await client.PostAsJsonAsync("/api/scopes", request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task InactiveUser_Returns403()
    {
        // Arrange
        await using var factory = CreateAuthTestFactory();
        var (userId, _) = await SeedUserAsync(factory, EditorRoleId, isActive: false);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestUserIdHeader, userId.ToString());

        // Act
        var response = await client.GetAsync("/api/scopes", TestCancellationToken);

        // Assert — IClaimsTransformation cannot fail authentication, so the result is
        // 403 (authenticated but denied) rather than 401 (unauthenticated).
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UserWithNoGrants_Returns403()
    {
        // Arrange
        await using var factory = CreateAuthTestFactory();
        var userId = Guid.CreateVersion7();
        using var scope = factory.Services.CreateScope();
        var roleStore = scope.ServiceProvider.GetRequiredService<IRoleStore>();
        var userStore = scope.ServiceProvider.GetRequiredService<IUserStore>();

        await roleStore.CreateAsync(ViewerRole, TestCancellationToken);

        var user = new User
        {
            Id = userId,
            Username = $"noaccess-{userId:N}",
            Email = $"{userId:N}@test.com",
            IsActive = true,
            Grants = [],
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await userStore.CreateAsync(user, TestCancellationToken);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestUserIdHeader, userId.ToString());

        // Act
        var response = await client.GetAsync("/api/scopes", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private GroundControlAuthTestFactory CreateAuthTestFactory() =>
        new(CreateFactory());

    private static async Task<(Guid UserId, User User)> SeedUserAsync(
        WebApplicationFactory<Program> factory,
        Guid roleId,
        bool isActive = true,
        Guid? resource = null)
    {
        using var scope = factory.Services.CreateScope();
        var roleStore = scope.ServiceProvider.GetRequiredService<IRoleStore>();
        var userStore = scope.ServiceProvider.GetRequiredService<IUserStore>();

        // Seed the role (idempotent — ignore if already exists)
        var role = roleId == ViewerRoleId ? ViewerRole : EditorRole;
        var existingRole = await roleStore.GetByIdAsync(roleId);
        if (existingRole is null)
        {
            await roleStore.CreateAsync(role);
        }

        var userId = Guid.CreateVersion7();
        var user = new User
        {
            Id = userId,
            Username = $"user-{userId:N}",
            Email = $"{userId:N}@test.com",
            IsActive = isActive,
            Grants = [new Grant { Resource = resource, RoleId = roleId }],
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await userStore.CreateAsync(user);
        return (userId, user);
    }

    /// <summary>
    /// Authentication handler for integration tests.
    /// Reads user ID from a custom header and creates a minimal principal.
    /// </summary>
    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "TestAuth";

        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var userIdHeader = Request.Headers[TestUserIdHeader].FirstOrDefault();
            if (userIdHeader is null || !Guid.TryParse(userIdHeader, out var userId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    /// <summary>
    /// Factory wrapper that replaces the default NoAuth scheme with a test auth scheme
    /// so the PermissionHandler resolves grants from the database.
    /// </summary>
    private sealed class GroundControlAuthTestFactory : WebApplicationFactory<Program>
    {
        private readonly GroundControlApiFactory _inner;

        public GroundControlAuthTestFactory(GroundControlApiFactory inner)
        {
            _inner = inner;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Let the inner factory configure MongoDB, logging, etc.
            _inner.WithWebHostBuilder(b => { }).CreateClient();

            builder.UseSetting("ConnectionStrings:Storage", _inner.Services.GetRequiredService<IConfiguration>()["ConnectionStrings:Storage"]);
            builder.UseSetting("Persistence:MongoDb:DatabaseName", _inner.Database.DatabaseNamespace.DatabaseName);
            builder.UseSetting("GroundControl:Security:AuthenticationMode", "None");

            builder.ConfigureServices(services =>
            {
                // Remove the NoAuth authentication scheme and replace with test scheme
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            });

            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddFilter(l => l >= LogLevel.Debug);
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}