using System.Net.Http.Json;
using System.Security.Claims;
using GroundControl.Api.Features.Clients.Contracts;
using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Api.Shared.Security.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Shared.Security.Authentication;

public sealed class ApiKeyAuthenticationHandlerTests : ApiHandlerTestBase
{
    public ApiKeyAuthenticationHandlerTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task Authenticate_WithValidApiKey_Succeeds()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();
        var project = await CreateProjectAsync(httpClient, "Auth Test Project");
        var created = await CreateClientAsync(httpClient, project.Id, "valid-client");

        // Act
        var result = await AuthenticateAsync(factory, $"ApiKey {created.Id}:{created.ClientSecret}");

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.Principal.ShouldNotBeNull();
        result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value.ShouldBe(created.Id.ToString());
        result.Principal.FindFirst("projectId")?.Value.ShouldBe(project.Id.ToString());
    }

    [Fact]
    public async Task Authenticate_WithValidApiKey_IncludesScopeClaims()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();
        var project = await CreateProjectAsync(httpClient, "Scoped Auth Project");
        await CreateScopeAsync(httpClient, "environment", ["dev", "prod"]);
        await CreateScopeAsync(httpClient, "region", ["us-east", "eu-west"]);

        var created = await CreateClientWithScopesAsync(
            httpClient,
            project.Id,
            "scoped-client",
            new Dictionary<string, string> { ["environment"] = "prod", ["region"] = "us-east" });

        // Act
        var result = await AuthenticateAsync(factory, $"ApiKey {created.Id}:{created.ClientSecret}");

        // Assert
        result.Succeeded.ShouldBeTrue();
        var scopeClaims = result.Principal!.FindAll("clientScope").Select(c => c.Value).ToList();
        scopeClaims.ShouldContain("environment:prod");
        scopeClaims.ShouldContain("region:us-east");
    }

    [Fact]
    public async Task Authenticate_WithWrongSecret_Fails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();
        var project = await CreateProjectAsync(httpClient, "Auth Test Project");
        var created = await CreateClientAsync(httpClient, project.Id, "wrong-secret-client");

        // Act
        var result = await AuthenticateAsync(factory, $"ApiKey {created.Id}:completely-wrong-secret");

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
        result.Failure.Message.ShouldBe("Invalid credentials");
    }

    [Fact]
    public async Task Authenticate_WithDeactivatedClient_Fails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();
        var project = await CreateProjectAsync(httpClient, "Auth Test Project");
        var created = await CreateClientAsync(httpClient, project.Id, "deactivated-client");
        await DeactivateClientAsync(httpClient, project.Id, created.Id);

        // Act
        var result = await AuthenticateAsync(factory, $"ApiKey {created.Id}:{created.ClientSecret}");

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
        result.Failure.Message.ShouldBe("Client is deactivated");
    }

    [Fact]
    public async Task Authenticate_WithExpiredClient_Fails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();
        var project = await CreateProjectAsync(httpClient, "Auth Test Project");
        var created = await CreateClientAsync(httpClient, project.Id, "expired-client");
        await ExpireClientAsync(httpClient, project.Id, created.Id);

        // Act
        var result = await AuthenticateAsync(factory, $"ApiKey {created.Id}:{created.ClientSecret}");

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
        result.Failure.Message.ShouldBe("Client credentials have expired");
    }

    [Fact]
    public async Task Authenticate_WithMissingAuthorizationHeader_ReturnsNoResult()
    {
        // Arrange
        await using var factory = CreateFactory();

        // Act
        var result = await AuthenticateAsync(factory, authorizationHeader: null);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.None.ShouldBeTrue();
    }

    [Fact]
    public async Task Authenticate_WithMalformedHeader_Fails()
    {
        // Arrange
        await using var factory = CreateFactory();

        // Act
        var result = await AuthenticateAsync(factory, "ApiKey no-colon-separator");

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
        result.Failure.Message.ShouldBe("Invalid ApiKey format");
    }

    [Fact]
    public async Task Authenticate_WithInvalidClientIdFormat_Fails()
    {
        // Arrange
        await using var factory = CreateFactory();

        // Act
        var result = await AuthenticateAsync(factory, "ApiKey not-a-guid:some-secret");

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
        result.Failure.Message.ShouldBe("Invalid client ID format");
    }

    [Fact]
    public async Task Authenticate_WithNonExistentClientId_Fails()
    {
        // Arrange
        await using var factory = CreateFactory();
        var fakeId = Guid.CreateVersion7();

        // Act
        var result = await AuthenticateAsync(factory, $"ApiKey {fakeId}:some-secret");

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
        result.Failure.Message.ShouldBe("Invalid credentials");
    }

    [Fact]
    public async Task Authenticate_WithDifferentScheme_ReturnsNoResult()
    {
        // Arrange
        await using var factory = CreateFactory();

        // Act
        var result = await AuthenticateAsync(factory, "Bearer some-jwt-token");

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.None.ShouldBeTrue();
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(
        GroundControlApiFactory factory, string? authorizationHeader)
    {
        using var scope = factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        if (authorizationHeader is not null)
        {
            httpContext.Request.Headers.Authorization = authorizationHeader;
        }

        return await authService.AuthenticateAsync(httpContext, ApiKeyAuthenticationHandler.SchemeName);
    }

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient httpClient, string name)
    {
        var request = new CreateProjectRequest { Name = name, Description = $"{name} project" };

        var response = await httpClient.PostAsJsonAsync("/api/projects", request, WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        project.ShouldNotBeNull();

        return project;
    }

    private static async Task<CreateClientResponse> CreateClientAsync(HttpClient httpClient, Guid projectId, string name)
    {
        var request = new CreateClientRequest { Name = name };

        var response = await httpClient.PostAsJsonAsync(
                $"/api/projects/{projectId}/clients", request, WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var client = await response.Content.ReadFromJsonAsync<CreateClientResponse>(WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        client.ShouldNotBeNull();

        return client;
    }

    private static async Task<CreateClientResponse> CreateClientWithScopesAsync(
        HttpClient httpClient, Guid projectId, string name, Dictionary<string, string> scopes)
    {
        var request = new CreateClientRequest { Name = name, Scopes = scopes };

        var response = await httpClient.PostAsJsonAsync(
                $"/api/projects/{projectId}/clients", request, WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var client = await response.Content.ReadFromJsonAsync<CreateClientResponse>(WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        client.ShouldNotBeNull();

        return client;
    }

    private static async Task<ScopeResponse> CreateScopeAsync(
        HttpClient httpClient, string dimension, List<string> allowedValues)
    {
        var request = new CreateScopeRequest { Dimension = dimension, AllowedValues = allowedValues };

        var response = await httpClient.PostAsJsonAsync("/api/scopes", request, WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var scope = await response.Content.ReadFromJsonAsync<ScopeResponse>(WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        scope.ShouldNotBeNull();

        return scope;
    }

    private static async Task DeactivateClientAsync(HttpClient httpClient, Guid projectId, Guid clientId)
    {
        var getResponse = await httpClient.GetAsync(
            $"/api/projects/{projectId}/clients/{clientId}", TestCancellationToken);

        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/projects/{projectId}/clients/{clientId}");
        request.Content = JsonContent.Create(
            new UpdateClientRequest { Name = "deactivated-client", IsActive = false },
            options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        var response = await httpClient.SendAsync(request, TestCancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task ExpireClientAsync(HttpClient httpClient, Guid projectId, Guid clientId)
    {
        var getResponse = await httpClient.GetAsync(
            $"/api/projects/{projectId}/clients/{clientId}", TestCancellationToken);

        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/projects/{projectId}/clients/{clientId}");
        request.Content = JsonContent.Create(
            new UpdateClientRequest { Name = "expired-client", IsActive = true, ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) },
            options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        var response = await httpClient.SendAsync(request, TestCancellationToken);
        response.EnsureSuccessStatusCode();
    }
}