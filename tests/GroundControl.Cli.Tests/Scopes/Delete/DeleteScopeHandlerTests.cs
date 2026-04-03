using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Scopes.Delete;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Scopes.Delete;

public sealed class DeleteScopeHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithYes_DeletesWithoutConfirmation()
    {
        // Arrange
        var scopeId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetScopeHandlerAsync(scopeId, Arg.Any<CancellationToken>())
            .Returns(new ScopeResponse
            {
                Id = scopeId,
                Dimension = "Environment",
                AllowedValues = ["dev"],
                Version = 3,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new DeleteScopeOptions { Id = scopeId, Version = 3, Yes = true });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("deleted");
        await client.Received(1).DeleteScopeHandlerAsync(scopeId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_DeletesWithoutConfirmation()
    {
        // Arrange
        var scopeId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetScopeHandlerAsync(scopeId, Arg.Any<CancellationToken>())
            .Returns(new ScopeResponse
            {
                Id = scopeId,
                Dimension = "Region",
                AllowedValues = ["us-east"],
                Version = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new DeleteScopeOptions { Id = scopeId, Version = 2 },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).DeleteScopeHandlerAsync(scopeId, Arg.Any<CancellationToken>());
    }

    // Interactive confirmation declined test is not possible with MockShellBuilder because
    // Spectre.Console's ConfirmAsync requires real console input (ReadKey).

    [Fact]
    public async Task HandleAsync_NotFound_ShowsError()
    {
        // Arrange
        var scopeId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetScopeHandlerAsync(scopeId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Not Found", 404, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 404, Detail = "Scope not found." }, null));

        var handler = CreateHandler(shellBuilder, client,
            new DeleteScopeOptions { Id = scopeId });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Scope not found.");
    }

    private static DeleteScopeHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        DeleteScopeOptions options,
        bool noInteractive = false) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}