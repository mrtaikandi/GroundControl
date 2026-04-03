using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Snapshots.Get;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Snapshots.Get;

public sealed class GetSnapshotHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersSnapshotDetail()
    {
        // Arrange
        var snapshotId = Guid.CreateVersion7();
        var projectId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetSnapshotHandlerAsync(projectId, snapshotId, Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns(new SnapshotResponse
            {
                Id = snapshotId,
                ProjectId = projectId,
                SnapshotVersion = 3,
                Entries =
                [
                    new ResolvedEntryResponse
                    {
                        Key = "Database:ConnectionString",
                        ValueType = "String",
                        IsSensitive = false,
                        Values = [new ScopedValueResponse { Scopes = new Dictionary<string, string>(), Value = "Server=localhost" }]
                    }
                ],
                PublishedAt = DateTimeOffset.UtcNow,
                PublishedBy = Guid.CreateVersion7(),
                Description = "Test snapshot"
            });

        var handler = CreateHandler(shellBuilder, client, snapshotId, projectId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain(snapshotId.ToString());
        output.ShouldContain("Test snapshot");
        output.ShouldContain("Database:ConnectionString");
        output.ShouldContain("Server=localhost");
    }

    [Fact]
    public async Task HandleAsync_SensitiveValues_MaskedByDefault()
    {
        // Arrange
        var snapshotId = Guid.CreateVersion7();
        var projectId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetSnapshotHandlerAsync(projectId, snapshotId, Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns(new SnapshotResponse
            {
                Id = snapshotId,
                ProjectId = projectId,
                SnapshotVersion = 1,
                Entries =
                [
                    new ResolvedEntryResponse
                    {
                        Key = "Secret",
                        ValueType = "String",
                        IsSensitive = true,
                        Values = [new ScopedValueResponse { Scopes = new Dictionary<string, string>(), Value = "super-secret" }]
                    }
                ],
                PublishedAt = DateTimeOffset.UtcNow,
                PublishedBy = Guid.CreateVersion7()
            });

        var handler = CreateHandler(shellBuilder, client, snapshotId, projectId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("********");
        output.ShouldNotContain("super-secret");
    }

    [Fact]
    public async Task HandleAsync_NotFound_ShowsError()
    {
        // Arrange
        var snapshotId = Guid.CreateVersion7();
        var projectId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetSnapshotHandlerAsync(projectId, snapshotId, Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Not Found", 404, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 404, Detail = "Snapshot not found." }, null));

        var handler = CreateHandler(shellBuilder, client, snapshotId, projectId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Snapshot not found.");
    }

    private static GetSnapshotHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        Guid id,
        Guid projectId,
        OutputFormat outputFormat,
        bool? decrypt = null) =>
        new(
            shellBuilder.Build(),
            Options.Create(new GetSnapshotOptions { Id = id, ProjectId = projectId, Decrypt = decrypt }),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);
}