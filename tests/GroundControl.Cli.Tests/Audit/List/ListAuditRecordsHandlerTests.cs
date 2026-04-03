using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Audit.List;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Tests.Audit.List;

public sealed class ListAuditRecordsHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersTableWithColumns()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListAuditRecordsHandlerAsync(
                Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
                Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfAuditRecordResponse
            {
                Data =
                [
                    CreateRecord("Scope", "Created"),
                    CreateRecord("Group", "Updated")
                ],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).ListAuditRecordsHandlerAsync(
            Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
            Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_JsonOutput_RendersJsonArray()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListAuditRecordsHandlerAsync(
                Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
                Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfAuditRecordResponse
            {
                Data = [CreateRecord("Scope", "Created")],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, OutputFormat.Json);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("\"EntityType\"");
        output.ShouldContain("Scope");
    }

    [Fact]
    public async Task HandleAsync_WithEntityTypeFilter_PassesFilterToClient()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListAuditRecordsHandlerAsync(
                Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
                Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfAuditRecordResponse
            {
                Data = [CreateRecord("Scope", "Created")],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, OutputFormat.Table, entityType: "Scope");

        // Act
        await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        await client.Received(1).ListAuditRecordsHandlerAsync(
            "Scope",
            Arg.Any<Guid?>(),
            Arg.Any<Guid?>(),
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithEntityIdFilter_PassesFilterToClient()
    {
        // Arrange
        var entityId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListAuditRecordsHandlerAsync(
                Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
                Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfAuditRecordResponse
            {
                Data = [],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, OutputFormat.Table, entityId: entityId);

        // Act
        await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        await client.Received(1).ListAuditRecordsHandlerAsync(
            Arg.Any<string?>(),
            entityId,
            Arg.Any<Guid?>(),
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>());
    }

    private static ListAuditRecordsHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        OutputFormat outputFormat,
        string? entityType = null,
        Guid? entityId = null) =>
        new(
            shellBuilder.Build(),
            Options.Create(new ListAuditRecordsOptions { EntityType = entityType, EntityId = entityId }),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);

    private static AuditRecordResponse CreateRecord(string entityType, string action) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            EntityType = entityType,
            EntityId = Guid.CreateVersion7(),
            Action = action,
            PerformedBy = Guid.CreateVersion7(),
            Changes = [],
            PerformedAt = DateTimeOffset.UtcNow
        };
}