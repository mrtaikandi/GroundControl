using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Variables.Update;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Variables.Update;

public sealed class UpdateVariableHandlerTests
{
    [Fact]
    public async Task HandleAsync_UpdatesVariable()
    {
        // Arrange
        var varId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.UpdateVariableHandlerAsync(varId, Arg.Any<UpdateVariableRequest>(), Arg.Any<CancellationToken>())
            .Returns(new VariableResponse
            {
                Id = varId,
                Name = "DbPassword",
                Scope = VariableScope.Global,
                IsSensitive = true,
                Values = [new ScopedValue { Scopes = null, Value = "new-secret" }],
                Version = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateVariableOptions { Id = varId, Values = ["default=new-secret"], Version = 1 });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("updated");
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
                Name = "SomeVar",
                Scope = VariableScope.Global,
                Values = [new ScopedValue { Scopes = null, Value = "old" }],
                Version = 5,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        client.UpdateVariableHandlerAsync(varId, Arg.Any<UpdateVariableRequest>(), Arg.Any<CancellationToken>())
            .Returns(new VariableResponse
            {
                Id = varId,
                Name = "SomeVar",
                Scope = VariableScope.Global,
                Values = [new ScopedValue { Scopes = null, Value = "new" }],
                Version = 6,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateVariableOptions { Id = varId, Values = ["default=new"] });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).GetVariableHandlerAsync(varId, Arg.Any<bool?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_InvalidValueFormat_ReturnsError()
    {
        // Arrange
        var varId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetVariableHandlerAsync(varId, Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns(new VariableResponse
            {
                Id = varId,
                Name = "SomeVar",
                Scope = VariableScope.Global,
                Values = [],
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateVariableOptions { Id = varId, Values = ["noequalssign"] });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Invalid scoped value format");
    }

    private static UpdateVariableHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        UpdateVariableOptions options,
        bool noInteractive = false) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}