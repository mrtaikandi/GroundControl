using System.Net;
using GroundControl.Cli.Shared.ApiClient;

namespace GroundControl.Cli.Tests.Shared;

public sealed class ApiVersionHandlerTests
{
    [Fact]
    public async Task SendAsync_AddsApiVersionHeader()
    {
        // Arrange
        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = new ApiVersionHandler { InnerHandler = innerHandler };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };

        HttpRequestMessage? capturedRequest = null;
        innerHandler.OnSend = req => capturedRequest = req;

        // Act
        await client.GetAsync("/api/test", TestContext.Current.CancellationToken);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Headers.TryGetValues("api-version", out var values).ShouldBeTrue();
        values!.Single().ShouldBe("1.0");
    }

    [Fact]
    public async Task SendAsync_DoesNotOverwriteExistingApiVersionHeader()
    {
        // Arrange
        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = new ApiVersionHandler { InnerHandler = innerHandler };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };

        HttpRequestMessage? capturedRequest = null;
        innerHandler.OnSend = req => capturedRequest = req;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/test");
        request.Headers.TryAddWithoutValidation("api-version", "2.0");

        // Act
        await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Headers.TryGetValues("api-version", out var values).ShouldBeTrue();
        values!.ShouldContain("2.0");
    }
}