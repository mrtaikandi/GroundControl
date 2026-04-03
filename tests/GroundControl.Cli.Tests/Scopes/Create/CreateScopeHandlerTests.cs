using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Scopes.Create;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Scopes.Create;

public sealed class CreateScopeHandlerTests
{
    [Fact]
    public async Task HandleAsync_AllOptions_CreatesScope()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateScopeHandlerAsync(Arg.Any<CreateScopeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ScopeResponse
            {
                Id = Guid.CreateVersion7(),
                Dimension = "Environment",
                AllowedValues = ["dev", "staging", "prod"],
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new CreateScopeOptions { Dimension = "Environment", Values = "dev,staging,prod" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("Environment");
        shellBuilder.GetOutput().ShouldContain("created");
        await client.Received(1).CreateScopeHandlerAsync(
            Arg.Is<CreateScopeRequest>(r =>
                r.Dimension == "Environment" &&
                r.AllowedValues.Count == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_MissingDimension_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateScopeOptions { Values = "dev" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("--dimension");
        await client.DidNotReceive().CreateScopeHandlerAsync(
            Arg.Any<CreateScopeRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_MissingValues_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateScopeOptions { Dimension = "Env" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("--values");
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_MissingBoth_ListsAllMissing()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateScopeOptions(),
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("--dimension");
        output.ShouldContain("--values");
    }

    // Interactive create prompt tests are not possible with MockShellBuilder because
    // Spectre.Console's PromptForStringAsync requires real console input (ReadKey).

    [Fact]
    public async Task HandleAsync_ApiValidationError_ShowsProblemDetails()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateScopeHandlerAsync(Arg.Any<CreateScopeRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<HttpValidationProblemDetails>(
                "Bad Request", 400, null, new Dictionary<string, IEnumerable<string>>(),
                new HttpValidationProblemDetails
                {
                    Status = 400,
                    Detail = "Validation failed.",
                    Errors = new Dictionary<string, ICollection<string>>
                    {
                        ["Dimension"] = ["Dimension is required."]
                    }
                }, null));

        var handler = CreateHandler(shellBuilder, client,
            new CreateScopeOptions { Dimension = "", Values = "dev" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Validation failed");
    }

    private static CreateScopeHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        CreateScopeOptions options,
        bool noInteractive) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}