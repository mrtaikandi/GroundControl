using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Variables.Create;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Variables.Create;

public sealed class CreateVariableHandlerTests
{
    [Fact]
    public async Task HandleAsync_AllOptions_CreatesVariable()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateVariableHandlerAsync(Arg.Any<CreateVariableRequest>(), Arg.Any<CancellationToken>())
            .Returns(new VariableResponse
            {
                Id = Guid.CreateVersion7(),
                Name = "DbPassword",
                Scope = VariableScope.Global,
                IsSensitive = true,
                Values = [new ScopedValue { Scopes = null, Value = "secret123" }],
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new CreateVariableOptions
            {
                Name = "DbPassword",
                Scope = VariableScope.Global,
                Sensitive = true,
                Values = ["default=secret123"]
            },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("DbPassword");
        shellBuilder.GetOutput().ShouldContain("created");
        await client.Received(1).CreateVariableHandlerAsync(
            Arg.Is<CreateVariableRequest>(r =>
                r.Name == "DbPassword" &&
                r.Scope == VariableScope.Global &&
                r.IsSensitive == true &&
                r.Values.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_MissingName_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateVariableOptions { Scope = VariableScope.Global },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("--name");
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_MissingBoth_ListsAllMissing()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateVariableOptions(),
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("--name");
        output.ShouldContain("--scope");
    }

    [Fact]
    public async Task HandleAsync_InvalidValueFormat_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateVariableOptions
            {
                Name = "SomeVar",
                Scope = VariableScope.Global,
                Values = ["badformat"]
            },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Invalid scoped value format");
    }

    [Fact]
    public async Task HandleAsync_ApiValidationError_ShowsProblemDetails()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateVariableHandlerAsync(Arg.Any<CreateVariableRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<HttpValidationProblemDetails>(
                "Bad Request", 400, null, new Dictionary<string, IEnumerable<string>>(),
                new HttpValidationProblemDetails
                {
                    Status = 400,
                    Detail = "Validation failed.",
                    Errors = new Dictionary<string, ICollection<string>>
                    {
                        ["Name"] = ["Name is required."]
                    }
                }, null));

        var handler = CreateHandler(shellBuilder, client,
            new CreateVariableOptions
            {
                Name = "",
                Scope = VariableScope.Global
            },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Validation failed");
    }

    private static CreateVariableHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        CreateVariableOptions options,
        bool noInteractive) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}