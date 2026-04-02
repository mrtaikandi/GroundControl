using System.Net;
using GroundControl.Api.Client.Contracts;
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

        var problem = new HttpValidationProblemDetails
        {
            Title = "Validation failed",
            Status = (int)HttpStatusCode.BadRequest,
            Detail = "Validation failed.",
            Errors = new Dictionary<string, ICollection<string>>
            {
                ["Name"] = ["Name is required."]
            }
        };

        // Act
        shell.RenderProblemDetails(problem);

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
        var problem = new ProblemDetails
        {
            Title = "Not Found",
            Status = 404,
            Detail = "Scope 'abc' was not found."
        };

        // Act
        shell.RenderProblemDetails(problem);

        // Assert
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Scope 'abc' was not found.");
    }

    [Fact]
    public void ProblemDetailsApiException()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();
        var problem = new ProblemDetails
        {
            Title = "Not Found",
            Status = 404,
            Detail = null
        };

        // Act
        shell.RenderProblemDetails(problem);

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
        var problem = new ProblemDetails
        {
            Title = "Conflict",
            Status = 409,
            Detail = "Version conflict."
        };

        // Act
        shell.RenderProblemDetails(problem);

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
        var problem = new ProblemDetails
        {
            Title = "Unprocessable Entity",
            Status = 422,
            Detail = "Variable references could not be resolved."
        };

        // Act
        shell.RenderProblemDetails(problem);

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
        var problem = new ProblemDetails
        {
            Title = "Precondition Required",
            Status = 428,
            Detail = "The If-Match header is required."
        };

        // Act
        shell.RenderProblemDetails(problem);

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
        var problem = new ProblemDetails
        {
            Title = "Internal Server Error",
            Status = 500,
            Detail = "Something went wrong."
        };

        // Act
        shell.RenderProblemDetails(problem);

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

        var problem = new HttpValidationProblemDetails
        {
            Title = "Validation failed",
            Status = (int)HttpStatusCode.BadRequest,
            Detail = "Validation failed.",
            Errors = new Dictionary<string, ICollection<string>>
            {
                ["Name"] = ["Name is required.", "Name must be at most 100 characters."],
                [""] = ["At least one value is required."]
            }
        };

        // Act
        shell.RenderProblemDetails(problem);

        // Assert
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Name: Name is required.");
        output.ShouldContain("Name: Name must be at most 100 characters.");
        output.ShouldContain("At least one value is required.");
    }
}