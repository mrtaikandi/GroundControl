using GroundControl.Api.Core;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core;

public sealed class EntityTagHeadersTests
{
    [Fact]
    public void ValidateIfMatch_WithValidHeader_ReturnsSuccess()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["If-Match"] = "\"42\"";

        // Act
        var result = EntityTagHeaders.ValidateIfMatch(httpContext);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ValidateIfMatch_WithMissingHeader_ReturnsFailedWithStatus428()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();

        // Act
        var result = EntityTagHeaders.ValidateIfMatch(httpContext);

        // Assert
        result.IsFailed.ShouldBeTrue();
        var problem = result.ToProblemDetails();
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status428PreconditionRequired);
        problem.Detail.ShouldBe("If-Match header is required.");
    }

    [Fact]
    public void TryParseIfMatchWithProblem_WithValidHeader_ReturnsTrueAndNullProblem()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["If-Match"] = "\"7\"";

        // Act
        var success = EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion, out var problem);

        // Assert
        success.ShouldBeTrue();
        expectedVersion.ShouldBe(7);
        problem.ShouldBeNull();
    }

    [Fact]
    public void TryParseIfMatchWithProblem_WithMissingHeader_ReturnsFalseAndProblemResult()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();

        // Act
        var success = EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion, out var problem);

        // Assert
        success.ShouldBeFalse();
        expectedVersion.ShouldBe(0);
        problem.ShouldNotBeNull();
    }
}