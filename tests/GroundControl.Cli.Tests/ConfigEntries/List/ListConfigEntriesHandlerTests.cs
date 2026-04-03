using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.ConfigEntries.List;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Tests.ConfigEntries.List;

public sealed class ListConfigEntriesHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersTable()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        var ownerId = Guid.CreateVersion7();
        client.ListConfigEntriesHandlerAsync(
                Arg.Any<Guid?>(), Arg.Any<ConfigEntryOwnerType?>(), Arg.Any<string?>(),
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool?>(),
                Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfConfigEntryResponse
            {
                Data =
                [
                    CreateEntry("DbConn", ownerId, "String", false),
                    CreateEntry("ApiKey", ownerId, "String", true)
                ],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, new ListConfigEntriesOptions(), OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Id");
        output.ShouldContain("Key");
        output.ShouldContain("DbConn");
        output.ShouldContain("ApiKey");
    }

    [Fact]
    public async Task HandleAsync_JsonOutput_RendersAllFields()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        var ownerId = Guid.CreateVersion7();
        client.ListConfigEntriesHandlerAsync(
                Arg.Any<Guid?>(), Arg.Any<ConfigEntryOwnerType?>(), Arg.Any<string?>(),
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool?>(),
                Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfConfigEntryResponse
            {
                Data = [CreateEntry("Database:ConnectionString", ownerId, "String", false)],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, new ListConfigEntriesOptions(), OutputFormat.Json);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("\"Key\"");
        output.ShouldContain("Database:ConnectionString");
        output.ShouldContain("\"OwnerType\"");
        output.ShouldContain("\"ValueType\"");
        output.ShouldContain("\"IsSensitive\"");
    }

    [Fact]
    public async Task HandleAsync_PassesFilterOptions()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        var ownerId = Guid.CreateVersion7();
        client.ListConfigEntriesHandlerAsync(
                ownerId, ConfigEntryOwnerType.Template, "Database:",
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), true,
                Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfConfigEntryResponse
            {
                Data = [CreateEntry("Database:ConnectionString", ownerId, "String", false)],
                NextCursor = null
            });

        var options = new ListConfigEntriesOptions
        {
            OwnerId = ownerId,
            OwnerType = ConfigEntryOwnerType.Template,
            KeyPrefix = "Database:",
            Decrypt = true
        };

        var handler = CreateHandler(shellBuilder, client, options, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).ListConfigEntriesHandlerAsync(
            ownerId, ConfigEntryOwnerType.Template, "Database:",
            Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), true,
            Arg.Any<CancellationToken>());
    }

    private static ListConfigEntriesHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        ListConfigEntriesOptions options,
        OutputFormat outputFormat) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);

    private static ConfigEntryResponse CreateEntry(
        string key, Guid ownerId, string valueType, bool sensitive) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Key = key,
            OwnerId = ownerId,
            OwnerType = ConfigEntryOwnerType.Template,
            ValueType = valueType,
            Values = [new ScopedValue { Scopes = null, Value = "test-value" }],
            IsSensitive = sensitive,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
}