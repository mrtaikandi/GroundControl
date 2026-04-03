using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Users.Update;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Users.Update;

public sealed class UpdateUserHandlerTests
{
    [Fact]
    public async Task HandleAsync_UpdatesUser()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.UpdateUserHandlerAsync(userId, Arg.Any<UpdateUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UserResponse
            {
                Id = userId,
                Username = "alice-updated",
                Email = "alice@example.com",
                IsActive = true,
                Grants = [],
                Version = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateUserOptions { Id = userId, Username = "alice-updated", Version = 1 });

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
                Version = 5,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        client.UpdateUserHandlerAsync(userId, Arg.Any<UpdateUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UserResponse
            {
                Id = userId,
                Username = "alice-updated",
                Email = "alice@example.com",
                IsActive = true,
                Grants = [],
                Version = 6,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateUserOptions { Id = userId, Username = "alice-updated" });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).GetUserHandlerAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Conflict_NonInteractive_ShowsDiffAndFails()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        client.UpdateUserHandlerAsync(userId, Arg.Any<UpdateUserRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Conflict", 409, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 409, Detail = "Version conflict." }, null));

        client.GetUserHandlerAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new UserResponse
            {
                Id = userId,
                Username = "alice-server",
                Email = "alice-server@example.com",
                IsActive = true,
                Grants = [],
                Version = 10,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateUserOptions { Id = userId, Username = "alice-local", Version = 5 },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Version conflict");
        output.ShouldContain("alice-local");
        output.ShouldContain("alice-server");
        output.ShouldContain("10");
    }

    private static UpdateUserHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        UpdateUserOptions options,
        bool noInteractive = false) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}