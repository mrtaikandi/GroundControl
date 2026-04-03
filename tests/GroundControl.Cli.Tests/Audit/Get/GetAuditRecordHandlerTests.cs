using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Audit.Get;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Audit.Get;

public sealed class GetAuditRecordHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersRecordDetail()
    {
        // Arrange
        var recordId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetAuditRecordHandlerAsync(recordId, Arg.Any<CancellationToken>())
            .Returns(new AuditRecordResponse
            {
                Id = recordId,
                EntityType = "Scope",
                EntityId = Guid.CreateVersion7(),
                Action = "Updated",
                PerformedBy = Guid.CreateVersion7(),
                Changes = [],
                PerformedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client, recordId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Scope");
        output.ShouldContain("Updated");
        output.ShouldContain(recordId.ToString());
    }

    [Fact]
    public async Task HandleAsync_WithFieldChanges_RendersChangesTable()
    {
        // Arrange
        var recordId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetAuditRecordHandlerAsync(recordId, Arg.Any<CancellationToken>())
            .Returns(new AuditRecordResponse
            {
                Id = recordId,
                EntityType = "Scope",
                EntityId = Guid.CreateVersion7(),
                Action = "Updated",
                PerformedBy = Guid.CreateVersion7(),
                Changes =
                [
                    new FieldChangeResponse { Field = "Description", OldValue = "Old desc", NewValue = "New desc" },
                    new FieldChangeResponse { Field = "AllowedValues", OldValue = "dev", NewValue = "dev, staging" }
                ],
                PerformedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client, recordId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Field");
        output.ShouldContain("Old Value");
        output.ShouldContain("New Value");
        output.ShouldContain("Description");
        output.ShouldContain("Old desc");
        output.ShouldContain("New desc");
        output.ShouldContain("AllowedValues");
    }

    [Fact]
    public async Task HandleAsync_NotFound_ShowsError()
    {
        // Arrange
        var recordId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetAuditRecordHandlerAsync(recordId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Not Found", 404, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 404, Detail = "Audit record not found." }, null));

        var handler = CreateHandler(shellBuilder, client, recordId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Audit record not found.");
    }

    [Fact]
    public async Task HandleAsync_JsonOutput_RendersFullRecordWithChanges()
    {
        // Arrange
        var recordId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetAuditRecordHandlerAsync(recordId, Arg.Any<CancellationToken>())
            .Returns(new AuditRecordResponse
            {
                Id = recordId,
                EntityType = "Scope",
                EntityId = Guid.CreateVersion7(),
                Action = "Created",
                PerformedBy = Guid.CreateVersion7(),
                Changes =
                [
                    new FieldChangeResponse { Field = "Name", OldValue = null, NewValue = "New" }
                ],
                PerformedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client, recordId, OutputFormat.Json);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("\"Scope\"");
        output.ShouldContain("\"Name\"");
        output.ShouldContain("\"New\"");
    }

    private static GetAuditRecordHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        Guid id,
        OutputFormat outputFormat) =>
        new(
            shellBuilder.Build(),
            Options.Create(new GetAuditRecordOptions { Id = id }),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);
}