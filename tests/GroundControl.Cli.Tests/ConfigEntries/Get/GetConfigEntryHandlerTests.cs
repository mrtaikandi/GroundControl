using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.ConfigEntries.Get;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.ConfigEntries.Get;

public sealed class GetConfigEntryHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersEntryDetailWithScopedValues()
    {
        // Arrange
        var entryId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetConfigEntryHandlerAsync(entryId, Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns(new ConfigEntryResponse
            {
                Id = entryId,
                Key = "Database:ConnectionString",
                OwnerId = ownerId,
                OwnerType = ConfigEntryOwnerType.Template,
                ValueType = "String",
                Values =
                [
                    new ScopedValue { Scopes = null, Value = "localhost" },
                    new ScopedValue
                    {
                        Scopes = new Dictionary<string, string> { ["env"] = "prod" },
                        Value = "sql.prod.internal"
                    }
                ],
                IsSensitive = false,
                Description = "Main database connection",
                Version = 3,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client, entryId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Database:ConnectionString");
        output.ShouldContain("localhost");
        output.ShouldContain("sql.prod.internal");
        output.ShouldContain("env:prod");
        output.ShouldContain("default");
        output.ShouldContain("Main database connection");
        output.ShouldContain(entryId.ToString());
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

        var handler = CreateHandler(shellBuilder, client, entryId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Config entry not found.");
    }

    [Fact]
    public async Task HandleAsync_PassesDecryptOption()
    {
        // Arrange
        var entryId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetConfigEntryHandlerAsync(entryId, true, Arg.Any<CancellationToken>())
            .Returns(new ConfigEntryResponse
            {
                Id = entryId,
                Key = "Secret",
                OwnerId = Guid.CreateVersion7(),
                OwnerType = ConfigEntryOwnerType.Template,
                ValueType = "String",
                Values = [new ScopedValue { Scopes = null, Value = "decrypted-value" }],
                IsSensitive = true,
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client, entryId, OutputFormat.Table, decrypt: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).GetConfigEntryHandlerAsync(entryId, true, Arg.Any<CancellationToken>());
    }

    private static GetConfigEntryHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        Guid id,
        OutputFormat outputFormat,
        bool? decrypt = null) =>
        new(
            shellBuilder.Build(),
            Options.Create(new GetConfigEntryOptions { Id = id, Decrypt = decrypt }),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);
}