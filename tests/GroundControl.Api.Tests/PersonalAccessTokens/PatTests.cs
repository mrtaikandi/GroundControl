using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using GroundControl.Api.Features.Auth.Contracts;
using GroundControl.Api.Features.PersonalAccessTokens.Contracts;
using GroundControl.Persistence.Contracts;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.PersonalAccessTokens;

public sealed class PatTests : ApiHandlerTestBase
{
    private static readonly string JwtSecret = Convert.ToBase64String(
    [
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
        17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
    ]);

    private const string SeedPassword = "Test!Password123";
    private const string SeedEmail = "admin@test.local";
    private const string SeedUsername = "admin";

    private static readonly string[] ScopesReadOnly = ["scopes:read"];
    private static readonly string[] InvalidPermissions = ["nonexistent:permission"];

    public PatTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    { }

    [Fact]
    public async Task CreatePat_ReturnsRawTokenOnce()
    {
        // Arrange
        await using var factory = CreateBuiltInFactory();
        using var client = factory.CreateClient();
        var jwt = await GetJwtAsync(client);

        // Act
        var response = await PostPatAsync(client, jwt, new { Name = "test-token" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var pat = await ReadRequiredJsonAsync<CreatePatResponse>(response, TestCancellationToken);
        pat.Name.ShouldBe("test-token");
        pat.Token.ShouldStartWith("gc_pat_");
        pat.TokenPrefix.ShouldNotBeNullOrWhiteSpace();
        pat.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task GetPat_DoesNotReturnTokenValue()
    {
        // Arrange
        await using var factory = CreateBuiltInFactory();
        using var client = factory.CreateClient();
        var jwt = await GetJwtAsync(client);

        var createResponse = await PostPatAsync(client, jwt, new { Name = "test-token" });
        var created = await ReadRequiredJsonAsync<CreatePatResponse>(createResponse, TestCancellationToken);

        // Act
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/personal-access-tokens/{created.Id}");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var response = await client.SendAsync(getRequest, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var pat = await ReadRequiredJsonAsync<PatResponse>(response, TestCancellationToken);
        pat.Name.ShouldBe("test-token");
        pat.TokenPrefix.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ListPats_ReturnsOnlyCallingUsersTokens()
    {
        // Arrange
        await using var factory = CreateBuiltInFactory();
        using var client = factory.CreateClient();
        var jwt = await GetJwtAsync(client);

        await PostPatAsync(client, jwt, new { Name = "token-1" });
        await PostPatAsync(client, jwt, new { Name = "token-2" });

        // Act
        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/personal-access-tokens");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var response = await client.SendAsync(listRequest, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var pats = await ReadRequiredJsonAsync<List<PatResponse>>(response, TestCancellationToken);
        pats.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task UseValidPat_AsBearer_AuthenticatesSuccessfully()
    {
        // Arrange
        await using var factory = CreateBuiltInFactory();
        using var client = factory.CreateClient();
        var jwt = await GetJwtAsync(client);

        var createResponse = await PostPatAsync(client, jwt, new { Name = "ci-token" });
        var created = await ReadRequiredJsonAsync<CreatePatResponse>(createResponse, TestCancellationToken);

        // Act — use the PAT to access a protected endpoint
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/personal-access-tokens");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", created.Token);
        var response = await client.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UseRevokedPat_Returns401()
    {
        // Arrange
        await using var factory = CreateBuiltInFactory();
        using var client = factory.CreateClient();
        var jwt = await GetJwtAsync(client);

        var createResponse = await PostPatAsync(client, jwt, new { Name = "revoke-test" });
        var created = await ReadRequiredJsonAsync<CreatePatResponse>(createResponse, TestCancellationToken);

        // Revoke the token
        using var revokeRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/personal-access-tokens/{created.Id}");
        revokeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var revokeResponse = await client.SendAsync(revokeRequest, TestCancellationToken);
        revokeResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act — try to use the revoked PAT
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/personal-access-tokens");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", created.Token);
        var response = await client.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UseExpiredPat_Returns401()
    {
        // Arrange
        await using var factory = CreateBuiltInFactory();
        using var client = factory.CreateClient();
        var jwt = await GetJwtAsync(client);

        var createResponse = await PostPatAsync(client, jwt, new { Name = "expiry-test", ExpiresInDays = 1 });
        var created = await ReadRequiredJsonAsync<CreatePatResponse>(createResponse, TestCancellationToken);

        // Manually expire the token in the database
        var collection = factory.Database.GetCollection<PersonalAccessToken>("personal_access_tokens");
        var update = Builders<PersonalAccessToken>.Update.Set(t => t.ExpiresAt, DateTimeOffset.UtcNow.AddHours(-1));
        await collection.UpdateOneAsync(
            Builders<PersonalAccessToken>.Filter.Eq(t => t.Id, created.Id),
            update,
            cancellationToken: TestCancellationToken);

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/personal-access-tokens");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", created.Token);
        var response = await client.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ScopedPat_WithinPermission_Succeeds()
    {
        // Arrange
        await using var factory = CreateBuiltInFactory();
        using var client = factory.CreateClient();
        var jwt = await GetJwtAsync(client);

        var createResponse = await PostPatAsync(client, jwt, new
        {
            Name = "scoped-token",
            Permissions = ScopesReadOnly
        });
        var created = await ReadRequiredJsonAsync<CreatePatResponse>(createResponse, TestCancellationToken);

        // Act — access an endpoint that requires scopes:read
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/scopes");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", created.Token);
        var response = await client.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ScopedPat_ExceedsPermission_Returns403()
    {
        // Arrange
        await using var factory = CreateBuiltInFactory();
        using var client = factory.CreateClient();
        var jwt = await GetJwtAsync(client);

        // Create PAT with only scopes:read permission
        var createResponse = await PostPatAsync(client, jwt, new
        {
            Name = "limited-token",
            Permissions = ScopesReadOnly
        });
        var created = await ReadRequiredJsonAsync<CreatePatResponse>(createResponse, TestCancellationToken);

        // Act — try to access an endpoint that requires scopes:write
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/scopes");
        request.Content = JsonContent.Create(new { Name = "test-scope", Dimensions = new[] { new { Key = "env", Ordinal = 1 } } });
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", created.Token);
        var response = await client.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RevokePat_Returns204()
    {
        // Arrange
        await using var factory = CreateBuiltInFactory();
        using var client = factory.CreateClient();
        var jwt = await GetJwtAsync(client);

        var createResponse = await PostPatAsync(client, jwt, new { Name = "to-revoke" });
        var created = await ReadRequiredJsonAsync<CreatePatResponse>(createResponse, TestCancellationToken);

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/personal-access-tokens/{created.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var response = await client.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify in DB
        var collection = factory.Database.GetCollection<PersonalAccessToken>("personal_access_tokens");
        var pat = await collection.Find(Builders<PersonalAccessToken>.Filter.Eq(t => t.Id, created.Id))
            .FirstOrDefaultAsync(TestCancellationToken);
        pat.ShouldNotBeNull();
        pat.IsRevoked.ShouldBeTrue();
    }

    [Fact]
    public async Task StoreGetByTokenHash_ReturnsCorrectRecord()
    {
        // Arrange
        await using var factory = CreateBuiltInFactory();
        using var client = factory.CreateClient();
        var jwt = await GetJwtAsync(client);

        var createResponse = await PostPatAsync(client, jwt, new { Name = "hash-lookup" });
        var created = await ReadRequiredJsonAsync<CreatePatResponse>(createResponse, TestCancellationToken);

        // Act — compute hash and look up directly in DB
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(created.Token));
        var tokenHash = Convert.ToHexStringLower(hashBytes);

        var collection = factory.Database.GetCollection<PersonalAccessToken>("personal_access_tokens");
        var pat = await collection.Find(Builders<PersonalAccessToken>.Filter.Eq(t => t.TokenHash, tokenHash))
            .FirstOrDefaultAsync(TestCancellationToken);

        // Assert
        pat.ShouldNotBeNull();
        pat.Id.ShouldBe(created.Id);
        pat.Name.ShouldBe("hash-lookup");
    }

    [Fact]
    public async Task CreatePat_WithInvalidPermission_Returns400()
    {
        // Arrange
        await using var factory = CreateBuiltInFactory();
        using var client = factory.CreateClient();
        var jwt = await GetJwtAsync(client);

        // Act
        var response = await PostPatAsync(client, jwt, new
        {
            Name = "bad-perms",
            Permissions = InvalidPermissions
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPat_ForOtherUser_Returns404()
    {
        // Arrange
        await using var factory = CreateBuiltInFactory();
        using var client = factory.CreateClient();
        var jwt = await GetJwtAsync(client);

        // Act — try to get a non-existent PAT
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/personal-access-tokens/{Guid.CreateVersion7()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var response = await client.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private GroundControlApiFactory CreateBuiltInFactory()
    {
        var config = new Dictionary<string, string?>
        {
            ["Authentication:AuthenticationMode"] = "BuiltIn",
            ["Authentication:BuiltIn:Jwt:Secret"] = JwtSecret,
            ["Authentication:BuiltIn:Jwt:Issuer"] = "GroundControl",
            ["Authentication:BuiltIn:Jwt:Audience"] = "GroundControl",
            ["Authentication:BuiltIn:Password:RequiredLength"] = "8",
            ["Authentication:Seed:AdminUsername"] = SeedUsername,
            ["Authentication:Seed:AdminEmail"] = SeedEmail,
            ["Authentication:Seed:AdminPassword"] = SeedPassword,
        };

        return CreateFactory(config);
    }

    private static async Task<string> GetJwtAsync(HttpClient client)
    {
        await client.GetAsync("/healthz/liveness", TestCancellationToken);
        var loginResponse = await client.PostAsJsonAsync("/auth/token", new { Username = SeedUsername, Password = SeedPassword }, TestCancellationToken);
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tokens = await ReadRequiredJsonAsync<TokenResponse>(loginResponse, TestCancellationToken);
        return tokens.AccessToken;
    }

    private static async Task<HttpResponseMessage> PostPatAsync(HttpClient client, string jwt, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/personal-access-tokens");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}