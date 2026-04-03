using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Users.Delete;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Users.Delete;

public sealed class DeleteUserHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithYes_DeletesWithoutConfirmation()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetUserHandlerAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new UserResponse
            {
                Id = userId,
                Username = "alice",
                Email = "alice@example.com",
                IsActive = true,
                Grants = [],
                Version = 3,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new DeleteUserOptions { Id = userId, Version = 3, Yes = true });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("deleted");
        await client.Received(1).DeleteUserHandlerAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_DeletesWithoutConfirmation()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetUserHandlerAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new UserResponse
            {
                Id = userId,
                Username = "alice",
                Email = "alice@example.com",
                IsActive = true,
                Grants = [],
                Version = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new DeleteUserOptions { Id = userId, Version = 2 },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).DeleteUserHandlerAsync(userId, Arg.Any<CancellationToken>());
    }

    // Interactive confirmation declined test is not possible with MockShellBuilder because
    // Spectre.Console's ConfirmAsync requires real console input (ReadKey).

    [Fact]
    public async Task HandleAsync_NotFound_ShowsError()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetUserHandlerAsync(userId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Not Found", 404, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 404, Detail = "User not found." }, null));

        var handler = CreateHandler(shellBuilder, client,
            new DeleteUserOptions { Id = userId });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("User not found.");
    }

    private static DeleteUserHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        DeleteUserOptions options,
        bool noInteractive = false) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}