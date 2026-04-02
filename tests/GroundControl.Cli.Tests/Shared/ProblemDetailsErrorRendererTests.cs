using GroundControl.Cli.Shared.ErrorHandling;

namespace GroundControl.Cli.Tests.Shared;

public sealed class ProblemDetailsErrorRendererTests
{
    [Fact]
    public void RenderProblemDetails_400_ShowsValidationErrors()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();
        var errors = new Dictionary<string, string[]>
        {
            ["Name"] = ["Name is required."]
        };

        var ex = new ProblemDetailsApiException(400, "Validation failed", "Validation failed.", errors);

        // Act
        shell.RenderProblemDetails(ex);

        // Assert
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Validation failed");
        output.ShouldContain("Name: Name is required.");
    }

    [Fact]
    public void RenderProblemDetails_404_ShowsNotFoundMessage()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();
        var ex = new ProblemDetailsApiException(404, "Not Found", "Scope 'abc' was not found.");

        // Act
        shell.RenderProblemDetails(ex);

        // Assert
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Scope 'abc' was not found.");
    }

    [Fact]
    public void RenderProblemDetails_404_WithoutDetail_ShowsDefaultMessage()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();
        var ex = new ProblemDetailsApiException(404, "Not Found", null);

        // Act
        shell.RenderProblemDetails(ex);

        // Assert
        var output = shellBuilder.GetOutput();
        output.ShouldContain("The requested resource was not found.");
    }

    [Fact]
    public void RenderProblemDetails_409_ShowsConflictMessage()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();
        var ex = new ProblemDetailsApiException(409, "Conflict", "Version conflict.");

        // Act
        shell.RenderProblemDetails(ex);

        // Assert
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Version conflict.");
    }

    [Fact]
    public void RenderProblemDetails_422_ShowsSemanticError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();
        var ex = new ProblemDetailsApiException(422, "Unprocessable Entity", "Variable references could not be resolved.");

        // Act
        shell.RenderProblemDetails(ex);

        // Assert
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Variable references could not be resolved.");
    }

    [Fact]
    public void RenderProblemDetails_428_ShowsVersionHint()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();
        var ex = new ProblemDetailsApiException(428, "Precondition Required", "The If-Match header is required.");

        // Act
        shell.RenderProblemDetails(ex);

        // Assert
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Version required");
        output.ShouldContain("--version");
    }

    [Fact]
    public void RenderProblemDetails_500_ShowsServerErrorMessage()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();
        var ex = new ProblemDetailsApiException(500, "Internal Server Error", "Something went wrong.");

        // Act
        shell.RenderProblemDetails(ex);

        // Assert
        var output = shellBuilder.GetOutput();
        output.ShouldContain("server error");
        output.ShouldContain("--debug");
    }

    [Fact]
    public void RenderProblemDetails_400_WithMultipleErrors_ShowsAll()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();
        var errors = new Dictionary<string, string[]>
        {
            ["Name"] = ["Name is required.", "Name must be at most 100 characters."],
            [""] = ["At least one value is required."]
        };

        var ex = new ProblemDetailsApiException(400, "Validation failed", "Validation failed.", errors);

        // Act
        shell.RenderProblemDetails(ex);

        // Assert
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Name: Name is required.");
        output.ShouldContain("Name: Name must be at most 100 characters.");
        output.ShouldContain("At least one value is required.");
    }
}