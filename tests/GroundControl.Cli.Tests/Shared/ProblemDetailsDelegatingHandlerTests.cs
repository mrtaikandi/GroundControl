using System.Net;
using System.Text;
using GroundControl.Cli.Shared.ErrorHandling;

namespace GroundControl.Cli.Tests.Shared;

public sealed class ProblemDetailsDelegatingHandlerTests
{
    [Fact]
    public async Task SendAsync_SuccessResponse_ReturnsResponse()
    {
        // Arrange
        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = new ProblemDetailsDelegatingHandler { InnerHandler = innerHandler };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };

        // Act
        var response = await client.GetAsync("/api/test", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(400, "Validation failed", "One or more validation errors occurred.")]
    [InlineData(404, "Not Found", "Scope 'abc' was not found.")]
    [InlineData(409, "Conflict", "Version conflict.")]
    [InlineData(422, "Unprocessable Entity", "Variable references could not be resolved.")]
    [InlineData(428, "Precondition Required", "The If-Match header is required.")]
    [InlineData(500, "Internal Server Error", "An unexpected error occurred.")]
    public async Task SendAsync_ErrorWithProblemDetails_ThrowsProblemDetailsApiException(
        int statusCode, string title, string detail)
    {
        // Arrange
        var json = $$"""{"title":"{{title}}","detail":"{{detail}}","status":{{statusCode}}}""";
        var response = CreateProblemResponse((HttpStatusCode)statusCode, json);

        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", response);

        using var handler = new ProblemDetailsDelegatingHandler { InnerHandler = innerHandler };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };

        // Act
        var ex = await Should.ThrowAsync<ProblemDetailsApiException>(
            () => client.GetAsync("/api/test", TestContext.Current.CancellationToken));

        // Assert
        ex.StatusCode.ShouldBe(statusCode);
        ex.Title.ShouldBe(title);
        ex.Detail.ShouldBe(detail);
    }

    [Fact]
    public async Task SendAsync_ValidationErrorWithErrors_ParsesValidationErrors()
    {
        // Arrange
        var json = """
            {
                "title": "Validation failed",
                "detail": "One or more validation errors occurred.",
                "status": 400,
                "errors": {
                    "Name": ["Name is required.", "Name must be at most 100 characters."],
                    "Description": ["Description is too long."]
                }
            }
            """;

        var response = CreateProblemResponse(HttpStatusCode.BadRequest, json);

        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", response);

        using var handler = new ProblemDetailsDelegatingHandler { InnerHandler = innerHandler };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };

        // Act
        var ex = await Should.ThrowAsync<ProblemDetailsApiException>(
            () => client.GetAsync("/api/test", TestContext.Current.CancellationToken));

        // Assert
        ex.StatusCode.ShouldBe(400);
        ex.ValidationErrors.ShouldContainKey("Name");
        ex.ValidationErrors["Name"].Length.ShouldBe(2);
        ex.ValidationErrors.ShouldContainKey("Description");
    }

    [Fact]
    public async Task SendAsync_ErrorWithNonJsonContent_ThrowsWithStatusCode()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("plain text error", Encoding.UTF8, "text/plain"),
            ReasonPhrase = "Internal Server Error"
        };

        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", response);

        using var handler = new ProblemDetailsDelegatingHandler { InnerHandler = innerHandler };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };

        // Act
        var ex = await Should.ThrowAsync<ProblemDetailsApiException>(
            () => client.GetAsync("/api/test", TestContext.Current.CancellationToken));

        // Assert
        ex.StatusCode.ShouldBe(500);
        ex.Title.ShouldBe("Internal Server Error");
    }

    [Fact]
    public async Task SendAsync_ErrorWithMalformedJson_ThrowsWithStatusCode()
    {
        // Arrange
        var response = CreateProblemResponse(HttpStatusCode.BadRequest, "not valid json {{{");

        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", response);

        using var handler = new ProblemDetailsDelegatingHandler { InnerHandler = innerHandler };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };

        // Act
        var ex = await Should.ThrowAsync<ProblemDetailsApiException>(
            () => client.GetAsync("/api/test", TestContext.Current.CancellationToken));

        // Assert
        ex.StatusCode.ShouldBe(400);
    }

    private static HttpResponseMessage CreateProblemResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/problem+json")
        };
}