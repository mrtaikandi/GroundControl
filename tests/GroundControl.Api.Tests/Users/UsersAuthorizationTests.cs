using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using GroundControl.Api.Features.Users.Contracts;
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

namespace GroundControl.Api.Tests.Users;

public sealed class UsersAuthorizationTests : ApiHandlerTestBase
{
    private const string TestUserIdHeader = "X-Test-User-Id";
    private static readonly Guid UsersReaderRoleId = Guid.CreateVersion7();
    private static readonly Guid UsersWriterRoleId = Guid.CreateVersion7();
    private static readonly Guid NoPermissionsRoleId = Guid.CreateVersion7();

    private static readonly Role UsersReaderRole = new()
    {
        Id = UsersReaderRoleId,
        Name = "UsersReader",
        Permissions = [Permissions.UsersRead],
        Version = 1,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static readonly Role UsersWriterRole = new()
    {
        Id = UsersWriterRoleId,
        Name = "UsersWriter",
        Permissions = [Permissions.UsersRead, Permissions.UsersWrite],
        Version = 1,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static readonly Role NoPermissionsRole = new()
    {
        Id = NoPermissionsRoleId,
        Name = "NoPermissions",
        Permissions = [],
        Version = 1,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    public UsersAuthorizationTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task GetUser_OwnProfile_WithoutUsersRead_Returns200()
    {
        // Arrange
        await using var factory = CreateAuthTestFactory();
        var (userId, _) = await SeedUserAsync(factory, NoPermissionsRoleId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestUserIdHeader, userId.ToString());

        // Act
        var response = await client.GetAsync($"/api/users/{userId}", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUser_OtherProfile_WithoutUsersRead_Returns403()
    {
        // Arrange
        await using var factory = CreateAuthTestFactory();
        var (callerId, _) = await SeedUserAsync(factory, NoPermissionsRoleId);
        var (otherId, _) = await SeedUserAsync(factory, NoPermissionsRoleId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestUserIdHeader, callerId.ToString());

        // Act
        var response = await client.GetAsync($"/api/users/{otherId}", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUser_OtherProfile_WithUsersRead_Returns200()
    {
        // Arrange
        await using var factory = CreateAuthTestFactory();
        var (callerId, _) = await SeedUserAsync(factory, UsersReaderRoleId);
        var (otherId, _) = await SeedUserAsync(factory, UsersReaderRoleId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestUserIdHeader, callerId.ToString());

        // Act
        var response = await client.GetAsync($"/api/users/{otherId}", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PutUser_OwnUsername_WithoutUsersWrite_Returns200()
    {
        // Arrange
        await using var factory = CreateAuthTestFactory();
        var (userId, _) = await SeedUserAsync(factory, NoPermissionsRoleId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestUserIdHeader, userId.ToString());

        var getResponse = await client.GetAsync($"/api/users/{userId}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();
        var currentUser = await ReadUserAsync(getResponse, TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/users/{userId}");
        request.Content = JsonContent.Create(new UpdateUserRequest
        {
            Username = "new-username",
            Email = currentUser.Email
        }, options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await client.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ReadUserAsync(response, TestCancellationToken);
        updated.Username.ShouldBe("new-username");
    }

    [Fact]
    public async Task PutUser_OwnGrants_WithoutUsersWrite_Returns403()
    {
        // Arrange
        await using var factory = CreateAuthTestFactory();
        var (userId, _) = await SeedUserAsync(factory, NoPermissionsRoleId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestUserIdHeader, userId.ToString());

        var getResponse = await client.GetAsync($"/api/users/{userId}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();
        var currentUser = await ReadUserAsync(getResponse, TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/users/{userId}");
        request.Content = JsonContent.Create(new UpdateUserRequest
        {
            Username = currentUser.Username,
            Email = currentUser.Email,
            Grants = [new GrantDto { RoleId = Guid.CreateVersion7() }]
        }, options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await client.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutUser_OtherUser_WithoutUsersWrite_Returns403()
    {
        // Arrange
        await using var factory = CreateAuthTestFactory();
        var (callerId, _) = await SeedUserAsync(factory, NoPermissionsRoleId);
        var (otherId, otherUser) = await SeedUserAsync(factory, NoPermissionsRoleId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestUserIdHeader, callerId.ToString());

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/users/{otherId}");
        request.Content = JsonContent.Create(new UpdateUserRequest
        {
            Username = otherUser.Username,
            Email = otherUser.Email
        }, options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", "\"1\"");

        // Act
        var response = await client.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteUser_WithoutUsersWrite_Returns403()
    {
        // Arrange
        await using var factory = CreateAuthTestFactory();
        var (callerId, _) = await SeedUserAsync(factory, NoPermissionsRoleId);
        var (otherId, _) = await SeedUserAsync(factory, NoPermissionsRoleId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestUserIdHeader, callerId.ToString());

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/users/{otherId}");
        request.Headers.TryAddWithoutValidation("If-Match", "\"1\"");

        // Act
        var response = await client.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private AuthTestFactory CreateAuthTestFactory() => new(CreateFactory());

    private static async Task<(Guid UserId, User User)> SeedUserAsync(
        WebApplicationFactory<Program> factory,
        Guid roleId,
        bool isActive = true)
    {
        using var scope = factory.Services.CreateScope();
        var roleStore = scope.ServiceProvider.GetRequiredService<IRoleStore>();
        var userStore = scope.ServiceProvider.GetRequiredService<IUserStore>();

        // Seed the role (idempotent)
        var role = roleId == UsersReaderRoleId ? UsersReaderRole
            : roleId == UsersWriterRoleId ? UsersWriterRole
            : NoPermissionsRole;

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
            Grants = [new Grant { Resource = null, RoleId = roleId }],
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await userStore.CreateAsync(user);
        return (userId, user);
    }

    private static async Task<UserResponse> ReadUserAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        => await ReadRequiredJsonAsync<UserResponse>(response, cancellationToken).ConfigureAwait(false);

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

    private sealed class AuthTestFactory : WebApplicationFactory<Program>
    {
        private readonly GroundControlApiFactory _inner;

        public AuthTestFactory(GroundControlApiFactory inner)
        {
            _inner = inner;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _inner.WithWebHostBuilder(_ => { }).CreateClient();

            builder.UseSetting("ConnectionStrings:Storage", _inner.Services.GetRequiredService<IConfiguration>()["ConnectionStrings:Storage"]);
            builder.UseSetting("Persistence:MongoDb:DatabaseName", _inner.Database.DatabaseNamespace.DatabaseName);
            builder.UseSetting("GroundControl:Security:AuthenticationMode", "None");

            builder.ConfigureServices(services =>
            {
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