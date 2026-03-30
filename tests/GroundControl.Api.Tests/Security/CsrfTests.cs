using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroundControl.Api.Features.Authentication.Contracts;
using GroundControl.Api.Features.PersonalAccessTokens.Contracts;
using Microsoft.Net.Http.Headers;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Security;

public sealed class CsrfTests : ApiHandlerTestBase
{
    private const string CsrfCookieName = "XSRF-TOKEN";
    private const string CsrfHeaderName = "X-XSRF-TOKEN";

    private static readonly string[] AllowedValues = ["dev", "staging", "prod"];

    public CsrfTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    { }

    [Fact]
    public async Task CookieAuthPost_WithoutCsrfHeader_Returns403()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();
        await LoginViaCookieAsync(client);

        // Act
        var response = await client.PostAsJsonAsync("/api/scopes", CreateScopeBody(), TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CookieAuthPost_WithValidCsrfToken_Returns201()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();
        await LoginViaCookieAsync(client);
        var csrfToken = await ObtainCsrfTokenAsync(client);

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/scopes");
        request.Content = JsonContent.Create(CreateScopeBody());
        request.Headers.Add(CsrfHeaderName, csrfToken);
        var response = await client.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CookieAuthPut_WithoutCsrfHeader_Returns403()
    {
        // Arrange — CSRF validation runs before the handler, so no real resource needed
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();
        await LoginViaCookieAsync(client);
        await ObtainCsrfTokenAsync(client);

        // Act — PUT without CSRF header
        using var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/scopes/{Guid.CreateVersion7()}");
        putRequest.Content = JsonContent.Create(CreateScopeBody());
        var response = await client.SendAsync(putRequest, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CookieAuthDelete_WithoutCsrfHeader_Returns403()
    {
        // Arrange — CSRF validation runs before the handler, so no real resource needed
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();
        await LoginViaCookieAsync(client);
        await ObtainCsrfTokenAsync(client);

        // Act — DELETE without CSRF header
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/scopes/{Guid.CreateVersion7()}");
        var response = await client.SendAsync(deleteRequest, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BearerJwtPost_WithoutCsrfHeader_Succeeds()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();
        var jwt = await GetJwtAsync(client);

        // Act — POST with JWT Bearer, no CSRF header
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/scopes");
        request.Content = JsonContent.Create(CreateScopeBody());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var response = await client.SendAsync(request, TestCancellationToken);

        // Assert — Bearer auth is exempt from CSRF
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PatBearerPost_WithoutCsrfHeader_Succeeds()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();
        var jwt = await GetJwtAsync(client);

        // Create a PAT
        using var createPatRequest = new HttpRequestMessage(HttpMethod.Post, "/api/personal-access-tokens");
        createPatRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        createPatRequest.Content = JsonContent.Create(new { Name = "csrf-test-pat" });
        var createPatResponse = await client.SendAsync(createPatRequest, TestCancellationToken);
        var pat = await ReadRequiredJsonAsync<CreatePatResponse>(createPatResponse, TestCancellationToken);

        // Act — POST with PAT Bearer, no CSRF header
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/scopes");
        request.Content = JsonContent.Create(CreateScopeBody("pat-scope"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pat.Token);
        var response = await client.SendAsync(request, TestCancellationToken);

        // Assert — PAT auth is exempt from CSRF
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CookieAuthPost_WithInvalidCsrfToken_Returns403()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();
        await LoginViaCookieAsync(client);
        await ObtainCsrfTokenAsync(client);

        // Act — POST with an invalid CSRF token value
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/scopes");
        request.Content = JsonContent.Create(CreateScopeBody());
        request.Headers.Add(CsrfHeaderName, "completely-invalid-token-value");
        var response = await client.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CsrfTokenCookie_IsNotHttpOnly()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();
        await LoginViaCookieAsync(client);

        // Act — make a GET request to trigger CSRF token issuance
        var response = await client.GetAsync("/api/scopes", TestCancellationToken);

        // Assert — the XSRF-TOKEN cookie must NOT have HttpOnly flag
        var setCookieHeaders = response.Headers.GetValues("Set-Cookie").ToList();
        var csrfCookie = setCookieHeaders.FirstOrDefault(c => c.StartsWith($"{CsrfCookieName}=", StringComparison.OrdinalIgnoreCase));
        csrfCookie.ShouldNotBeNull("XSRF-TOKEN cookie should be set on authenticated response");

        var parsed = SetCookieHeaderValue.Parse(csrfCookie);
        parsed.HttpOnly.ShouldBeFalse("XSRF-TOKEN cookie must not be HttpOnly so JavaScript can read it");
    }

    [Fact]
    public async Task CsrfSettings_CanBeOverridden()
    {
        // Arrange
        var customCookieName = "MY-CSRF-TOKEN";
        var customHeaderName = "X-MY-CSRF";
        await using var factory = CreateApiFactoryWithBuiltInAuthentication(extraConfig: new Dictionary<string, string?>
        {
            ["Authentication:Csrf:CookieName"] = customCookieName,
            ["Authentication:Csrf:HeaderName"] = customHeaderName,
        });
        using var client = factory.CreateClient();
        await LoginViaCookieAsync(client);

        // Act — make a GET to trigger token issuance
        var response = await client.GetAsync("/api/scopes", TestCancellationToken);

        // Assert — custom cookie name is used
        var setCookieHeaders = response.Headers.GetValues("Set-Cookie").ToList();
        var csrfCookie = setCookieHeaders.FirstOrDefault(c => c.StartsWith($"{customCookieName}=", StringComparison.OrdinalIgnoreCase));
        csrfCookie.ShouldNotBeNull($"{customCookieName} cookie should be set when overridden via configuration");

        // Act — POST with custom header and token
        var csrfToken = ExtractCookieValue(response, customCookieName);
        csrfToken.ShouldNotBeNull();

        using var postRequest = new HttpRequestMessage(HttpMethod.Post, "/api/scopes");
        postRequest.Content = JsonContent.Create(CreateScopeBody("custom-csrf-scope"));
        postRequest.Headers.Add(customHeaderName, csrfToken);
        var postResponse = await client.SendAsync(postRequest, TestCancellationToken);

        // Assert
        postResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task LoginViaCookieAsync(HttpClient client)
    {
        await client.GetAsync("/healthz/liveness", TestCancellationToken);
        var response = await client.PostAsJsonAsync("/auth/login", new { Username = SeedUsername, Password = SeedPassword }, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<string> ObtainCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/scopes", TestCancellationToken);
        var csrfToken = ExtractCookieValue(response, CsrfCookieName);
        csrfToken.ShouldNotBeNull("CSRF token should be issued on authenticated request");
        return csrfToken;
    }

    private static async Task<string> GetJwtAsync(HttpClient client)
    {
        await client.GetAsync("/healthz/liveness", TestCancellationToken);
        var loginResponse = await client.PostAsJsonAsync("/auth/token", new { Username = SeedUsername, Password = SeedPassword }, TestCancellationToken);
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tokens = await ReadRequiredJsonAsync<TokenResponse>(loginResponse, TestCancellationToken);
        return tokens.AccessToken;
    }

    private static string? ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        var prefix = $"{cookieName}=";
        foreach (var cookie in cookies)
        {
            if (!cookie.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = cookie[prefix.Length..];
            var semicolonIndex = value.IndexOf(';', StringComparison.Ordinal);
            return semicolonIndex >= 0 ? value[..semicolonIndex] : value;
        }

        return null;
    }

    private static object CreateScopeBody(string dimension = "environment") => new
    {
        Dimension = dimension,
        AllowedValues
    };
}