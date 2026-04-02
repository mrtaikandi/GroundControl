using System.Net;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.OpenApi;

public sealed class OpenApiDocumentTests : ApiHandlerTestBase
{
    public OpenApiDocumentTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task OpenApiDocument_WhenRequested_ModelsValidationProblemDetailsAsProblemDetailsInheritance()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/openapi/v1.json", TestCancellationToken);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestCancellationToken));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var validationProblemDetailsSchema = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("HttpValidationProblemDetails");

        var allOf = validationProblemDetailsSchema.GetProperty("allOf");
        var errorsItems = allOf[1]
            .GetProperty("properties")
            .GetProperty("errors")
            .GetProperty("additionalProperties")
            .GetProperty("items");

        allOf.GetArrayLength().ShouldBe(2);
        allOf[0].GetProperty("$ref").GetString().ShouldBe("#/components/schemas/ProblemDetails");
        errorsItems.GetProperty("type").GetString().ShouldBe("string");
    }
}