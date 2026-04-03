using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Snapshots.Publish;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Snapshots.Publish;

public sealed class PublishSnapshotHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithProjectId_PublishesAndShowsSuccess()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.PublishSnapshotHandlerAsync(projectId, Arg.Any<PublishSnapshotRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SnapshotSummaryResponse
            {
                Id = Guid.CreateVersion7(),
                ProjectId = projectId,
                SnapshotVersion = 5,
                EntryCount = 12,
                PublishedAt = DateTimeOffset.UtcNow,
                PublishedBy = Guid.CreateVersion7()
            });

        var handler = CreateHandler(
            shellBuilder,
            client,
            new PublishSnapshotOptions { ProjectId = projectId, Description = "v1.0 release" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("version: 5");
        output.ShouldContain("entries: 12");
        await client.Received(1).PublishSnapshotHandlerAsync(
            projectId,
            Arg.Is<PublishSnapshotRequest>(r => r.Description == "v1.0 release"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NoProjectId_NonInteractive_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        var handler = CreateHandler(shellBuilder, client, new PublishSnapshotOptions(), noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("--project-id");
    }

    [Fact]
    public async Task HandleAsync_ApiError_ShowsProblemDetails()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.PublishSnapshotHandlerAsync(projectId, Arg.Any<PublishSnapshotRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Unprocessable Entity", 422, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 422, Detail = "Project has unresolved variables." }, null));

        var handler = CreateHandler(
            shellBuilder,
            client,
            new PublishSnapshotOptions { ProjectId = projectId },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Project has unresolved variables.");
    }

    private static PublishSnapshotHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        PublishSnapshotOptions options,
        bool noInteractive = false) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}