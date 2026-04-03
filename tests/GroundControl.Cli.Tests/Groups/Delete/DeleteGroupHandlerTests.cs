using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Groups.Delete;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Groups.Delete;

public sealed class DeleteGroupHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithYes_DeletesWithoutConfirmation()
    {
        // Arrange
        var groupId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetGroupHandlerAsync(groupId, Arg.Any<CancellationToken>())
            .Returns(new GroupResponse
            {
                Id = groupId,
                Name = "To Delete",
                Version = 3,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new DeleteGroupOptions { Id = groupId, Version = 3, Yes = true });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("deleted");
        await client.Received(1).DeleteGroupHandlerAsync(groupId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_DeletesWithoutConfirmation()
    {
        // Arrange
        var groupId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetGroupHandlerAsync(groupId, Arg.Any<CancellationToken>())
            .Returns(new GroupResponse
            {
                Id = groupId,
                Name = "To Delete",
                Version = 3,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new DeleteGroupOptions { Id = groupId, Version = 3 },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).DeleteGroupHandlerAsync(groupId, Arg.Any<CancellationToken>());
    }

    // Interactive confirmation declined test is not possible with MockShellBuilder because
    // Spectre.Console's ConfirmAsync requires real console input (ReadKey).
    // The decline flow is covered by the acceptance criteria at the integration level.

    [Fact]
    public async Task HandleAsync_NotFound_ShowsError()
    {
        // Arrange
        var groupId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetGroupHandlerAsync(groupId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Not Found", 404, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 404, Detail = "Group not found." }, null));

        var handler = CreateHandler(shellBuilder, client,
            new DeleteGroupOptions { Id = groupId });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Group not found.");
    }

    private static DeleteGroupHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        DeleteGroupOptions options,
        bool noInteractive = false) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}