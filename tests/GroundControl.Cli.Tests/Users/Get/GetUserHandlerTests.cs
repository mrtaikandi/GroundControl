using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Users.Get;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Users.Get;

public sealed class GetUserHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersUserDetail()
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
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client, new GetUserOptions { Id = userId });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("alice");
        output.ShouldContain("alice@example.com");
        output.ShouldContain(userId.ToString());
    }

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

        var handler = CreateHandler(shellBuilder, client, new GetUserOptions { Id = userId });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("User not found.");
    }

    private static GetUserHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        GetUserOptions options) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions()),
            client);
}