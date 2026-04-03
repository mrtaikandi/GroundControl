using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.ConfigEntries.Delete;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.ConfigEntries.Delete;

public sealed class DeleteConfigEntryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithYes_DeletesWithoutConfirmation()
    {
        // Arrange
        var entryId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new DeleteConfigEntryOptions { Id = entryId, Version = 3, Yes = true });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("deleted");
        await client.Received(1).DeleteConfigEntryHandlerAsync(entryId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_DeletesWithoutConfirmation()
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
                Version = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new DeleteConfigEntryOptions { Id = entryId, Version = 2 },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).DeleteConfigEntryHandlerAsync(entryId, Arg.Any<CancellationToken>());
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
                Key = "Database:ConnectionString",
                OwnerId = Guid.CreateVersion7(),
                OwnerType = ConfigEntryOwnerType.Template,
                ValueType = "String",
                Values = [],
                Version = 7,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new DeleteConfigEntryOptions { Id = entryId, Yes = true });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).GetConfigEntryHandlerAsync(entryId, Arg.Any<bool?>(), Arg.Any<CancellationToken>());
        await client.Received(1).DeleteConfigEntryHandlerAsync(entryId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NotFound_ShowsError()
    {
        // Arrange
        var entryId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetConfigEntryHandlerAsync(entryId, Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Not Found", 404, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 404, Detail = "Config entry not found." }, null));

        var handler = CreateHandler(shellBuilder, client,
            new DeleteConfigEntryOptions { Id = entryId });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Config entry not found.");
    }

    private static DeleteConfigEntryHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        DeleteConfigEntryOptions options,
        bool noInteractive = false) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}