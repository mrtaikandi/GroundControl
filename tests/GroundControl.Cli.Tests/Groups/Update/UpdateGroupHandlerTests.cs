using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Groups.Update;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Groups.Update;

public sealed class UpdateGroupHandlerTests
{
    [Fact]
    public async Task HandleAsync_UpdatesGroup()
    {
        // Arrange
        var groupId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.UpdateGroupHandlerAsync(groupId, Arg.Any<UpdateGroupRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GroupResponse
            {
                Id = groupId,
                Name = "Updated Group",
                Version = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateGroupOptions { Id = groupId, Name = "Updated Group", Version = 1 });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("Updated Group");
        shellBuilder.GetOutput().ShouldContain("updated");
    }

    [Fact]
    public async Task HandleAsync_NoVersion_FetchesCurrentFirst()
    {
        // Arrange
        var groupId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetGroupHandlerAsync(groupId, Arg.Any<CancellationToken>())
            .Returns(new GroupResponse
            {
                Id = groupId,
                Name = "Current",
                Version = 5,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        client.UpdateGroupHandlerAsync(groupId, Arg.Any<UpdateGroupRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GroupResponse
            {
                Id = groupId,
                Name = "Updated",
                Version = 6,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateGroupOptions { Id = groupId, Name = "Updated" });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).GetGroupHandlerAsync(groupId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Conflict_NonInteractive_ShowsDiffAndFails()
    {
        // Arrange
        var groupId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        client.UpdateGroupHandlerAsync(groupId, Arg.Any<UpdateGroupRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Conflict", 409, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 409, Detail = "Version conflict." }, null));

        client.GetGroupHandlerAsync(groupId, Arg.Any<CancellationToken>())
            .Returns(new GroupResponse
            {
                Id = groupId,
                Name = "Server Name",
                Description = "Server Desc",
                Version = 10,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateGroupOptions { Id = groupId, Name = "My Name", Version = 5 },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Version conflict");
        output.ShouldContain("My Name");
        output.ShouldContain("Server Name");
        output.ShouldContain("10");
    }

    private static UpdateGroupHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        UpdateGroupOptions options,
        bool noInteractive = false) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}