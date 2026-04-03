using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Snapshots.List;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Tests.Snapshots.List;

public sealed class ListSnapshotsHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithProjectId_RendersTable()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListSnapshotsHandlerAsync(
                projectId,
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfSnapshotSummaryResponse
            {
                Data =
                [
                    CreateSnapshot(projectId, 1, 5, "Initial release"),
                    CreateSnapshot(projectId, 2, 8, null)
                ],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, new ListSnapshotsOptions { ProjectId = projectId }, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).ListSnapshotsHandlerAsync(
            projectId,
            Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_JsonOutput_RendersJsonArray()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListSnapshotsHandlerAsync(
                projectId,
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfSnapshotSummaryResponse
            {
                Data = [CreateSnapshot(projectId, 1, 3, "Test")],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, new ListSnapshotsOptions { ProjectId = projectId }, OutputFormat.Json);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("\"SnapshotVersion\"");
        output.ShouldContain("\"EntryCount\"");
    }

    [Fact]
    public async Task HandleAsync_NoProjectId_NonInteractive_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        var handler = CreateHandler(shellBuilder, client, new ListSnapshotsOptions(), OutputFormat.Table, noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("--project-id");
    }

    private static ListSnapshotsHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        ListSnapshotsOptions options,
        OutputFormat outputFormat,
        bool noInteractive = false) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat, NoInteractive = noInteractive }),
            client);

    private static SnapshotSummaryResponse CreateSnapshot(Guid projectId, long version, int entryCount, string? description) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            SnapshotVersion = version,
            EntryCount = entryCount,
            PublishedAt = DateTimeOffset.UtcNow,
            PublishedBy = Guid.CreateVersion7(),
            Description = description
        };
}