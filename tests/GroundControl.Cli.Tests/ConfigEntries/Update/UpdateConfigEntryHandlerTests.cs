using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.ConfigEntries.Update;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.ConfigEntries.Update;

public sealed class UpdateConfigEntryHandlerTests
{
    [Fact]
    public async Task HandleAsync_UpdatesEntry()
    {
        // Arrange
        var entryId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetConfigEntryHandlerAsync(entryId, Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns(new ConfigEntryResponse
            {
                Id = entryId,
                Key = "Database:ConnectionString",
                OwnerId = Guid.CreateVersion7(),
                OwnerType = ConfigEntryOwnerType.Template,
                ValueType = "String",
                Values = [new ScopedValue { Scopes = null, Value = "old-value" }],
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        client.UpdateConfigEntryHandlerAsync(entryId, Arg.Any<UpdateConfigEntryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ConfigEntryResponse
            {
                Id = entryId,
                Key = "Database:ConnectionString",
                OwnerId = Guid.CreateVersion7(),
                OwnerType = ConfigEntryOwnerType.Template,
                ValueType = "String",
                Values = [new ScopedValue { Scopes = null, Value = "new-value" }],
                Version = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateConfigEntryOptions
            {
                Id = entryId,
                Values = ["default=new-value"],
                Version = 1
            });

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
        var entryId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetConfigEntryHandlerAsync(entryId, Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns(new ConfigEntryResponse
            {
                Id = entryId,
                Key = "SomeKey",
                OwnerId = Guid.CreateVersion7(),
                OwnerType = ConfigEntryOwnerType.Template,
                ValueType = "String",
                Values = [new ScopedValue { Scopes = null, Value = "old" }],
                Version = 5,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        client.UpdateConfigEntryHandlerAsync(entryId, Arg.Any<UpdateConfigEntryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ConfigEntryResponse
            {
                Id = entryId,
                Key = "SomeKey",
                OwnerId = Guid.CreateVersion7(),
                OwnerType = ConfigEntryOwnerType.Template,
                ValueType = "String",
                Values = [new ScopedValue { Scopes = null, Value = "new" }],
                Version = 6,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateConfigEntryOptions { Id = entryId, Values = ["default=new"] });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).GetConfigEntryHandlerAsync(entryId, Arg.Any<bool?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Conflict_NonInteractive_ShowsDiffAndFails()
    {
        // Arrange
        var entryId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        client.UpdateConfigEntryHandlerAsync(entryId, Arg.Any<UpdateConfigEntryRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Conflict", 409, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 409, Detail = "Version conflict." }, null));

        client.GetConfigEntryHandlerAsync(entryId, Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns(new ConfigEntryResponse
            {
                Id = entryId,
                Key = "SomeKey",
                OwnerId = Guid.CreateVersion7(),
                OwnerType = ConfigEntryOwnerType.Template,
                ValueType = "Int32",
                Values = [new ScopedValue { Scopes = null, Value = "42" }],
                Version = 10,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateConfigEntryOptions
            {
                Id = entryId,
                ValueType = "String",
                Version = 5
            },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Version conflict");
        output.ShouldContain("String");
        output.ShouldContain("Int32");
        output.ShouldContain("10");
    }

    [Fact]
    public async Task HandleAsync_InvalidValueFormat_ReturnsError()
    {
        // Arrange
        var entryId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetConfigEntryHandlerAsync(entryId, Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns(new ConfigEntryResponse
            {
                Id = entryId,
                Key = "SomeKey",
                OwnerId = Guid.CreateVersion7(),
                OwnerType = ConfigEntryOwnerType.Template,
                ValueType = "String",
                Values = [],
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateConfigEntryOptions
            {
                Id = entryId,
                Values = ["noequalssign"]
            });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Invalid scoped value format");
    }

    private static UpdateConfigEntryHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        UpdateConfigEntryOptions options,
        bool noInteractive = false) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}