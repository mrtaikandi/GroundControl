using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using GroundControl.Persistence.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.Authentication;

public sealed class BuiltInAuthTests : ApiHandlerTestBase
{
    public BuiltInAuthTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    { }

    [Fact]
    public async Task Startup_InBuiltInMode_SeedsAdminUserInBothCollections()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();

        // Act — trigger host startup so hosted services run
        await client.GetAsync("/healthz/liveness", TestCancellationToken);

        // Assert — domain user exists
        var usersCollection = factory.Database.GetCollection<User>("users");
        var domainUser = await usersCollection
            .Find(Builders<User>.Filter.Eq(u => u.Email, SeedEmail))
            .FirstOrDefaultAsync(TestCancellationToken);

        domainUser.ShouldNotBeNull();
        domainUser.Username.ShouldBe(SeedUsername);
        domainUser.Email.ShouldBe(SeedEmail);
        domainUser.IsActive.ShouldBeTrue();
        domainUser.Grants.ShouldNotBeEmpty();
        domainUser.Grants[0].Resource.ShouldBeNull(); // system-wide
    }

    [Fact]
    public async Task Startup_SecondTime_DoesNotCreateDuplicateAdmin()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();
        await client.GetAsync("/healthz/liveness", TestCancellationToken);

        // Act
        await using var factory2 = CreateApiFactoryWithBuiltInAuthentication(existingDatabase: factory.Database);
        using var client2 = factory2.CreateClient();
        await client2.GetAsync("/healthz/liveness", TestCancellationToken);

        // Assert
        var usersCollection = factory.Database.GetCollection<User>("users");
        var count = await usersCollection
            .CountDocumentsAsync(Builders<User>.Filter.Eq(u => u.Email, SeedEmail), cancellationToken: TestCancellationToken);

        count.ShouldBe(1);
    }

    [Fact]
    public async Task Request_WithValidJwt_ReturnsOk()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();
        await client.GetAsync("/healthz/liveness", TestCancellationToken);

        var domainUser = await GetSeededAdminUserAsync(factory.Database);
        var token = GenerateJwtToken(domainUser!.Id);

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/scopes");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Request_WithExpiredJwt_ReturnsUnauthorized()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();

        var token = GenerateJwtToken(Guid.CreateVersion7(), expires: DateTime.UtcNow.AddHours(-1));

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/scopes");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_WithNoCredentials_ReturnsUnauthorized()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/scopes", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_WithInvalidJwt_ReturnsUnauthorized()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/scopes");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.jwt.token");
        var response = await client.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CookieRedirect_IsSuppressed_Returns401NotRedirect()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Act — request without Authorization header uses cookie scheme
        var response = await client.GetAsync("/api/scopes", TestCancellationToken);

        // Assert — should be 401, not 302 redirect
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<User?> GetSeededAdminUserAsync(IMongoDatabase database)
    {
        var usersCollection = database.GetCollection<User>("users");
        return await usersCollection
            .Find(Builders<User>.Filter.Eq(u => u.Email, SeedEmail))
            .FirstOrDefaultAsync();
    }

    private static string GenerateJwtToken(Guid userId, DateTime? expires = null)
    {
        var key = new SymmetricSecurityKey(Convert.FromBase64String(JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: "GroundControl",
            audience: "GroundControl",
            claims: claims,
            expires: expires ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}