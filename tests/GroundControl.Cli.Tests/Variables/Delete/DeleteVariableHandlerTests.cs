using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Variables.Delete;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Variables.Delete;

public sealed class DeleteVariableHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithYes_DeletesWithoutConfirmation()
    {
        // Arrange
        var varId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new DeleteVariableOptions { Id = varId, Version = 3, Yes = true });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("deleted");
        await client.Received(1).DeleteVariableHandlerAsync(varId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NoVersion_FetchesCurrentFirst()
    {
        // Arrange
        var varId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetVariableHandlerAsync(varId, Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns(new VariableResponse
            {
                Id = varId,
                Name = "DbPassword",
                Scope = VariableScope.Global,
                Values = [],
                Version = 7,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new DeleteVariableOptions { Id = varId, Yes = true });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).GetVariableHandlerAsync(varId, Arg.Any<bool?>(), Arg.Any<CancellationToken>());
        await client.Received(1).DeleteVariableHandlerAsync(varId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NotFound_ShowsError()
    {
        // Arrange
        var varId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetVariableHandlerAsync(varId, Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Not Found", 404, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 404, Detail = "Variable not found." }, null));

        var handler = CreateHandler(shellBuilder, client,
            new DeleteVariableOptions { Id = varId });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Variable not found.");
    }

    private static DeleteVariableHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        DeleteVariableOptions options,
        bool noInteractive = false) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}