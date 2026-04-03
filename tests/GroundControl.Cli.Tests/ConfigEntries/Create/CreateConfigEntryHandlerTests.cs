using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.ConfigEntries.Create;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.ConfigEntries.Create;

public sealed class CreateConfigEntryHandlerTests
{
    [Fact]
    public async Task HandleAsync_AllOptions_CreatesEntry()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        var ownerId = Guid.CreateVersion7();
        client.CreateConfigEntryHandlerAsync(Arg.Any<CreateConfigEntryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ConfigEntryResponse
            {
                Id = Guid.CreateVersion7(),
                Key = "Database:ConnectionString",
                OwnerId = ownerId,
                OwnerType = ConfigEntryOwnerType.Template,
                ValueType = "String",
                Values = [new ScopedValue { Scopes = null, Value = "localhost" }],
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new CreateConfigEntryOptions
            {
                Key = "Database:ConnectionString",
                OwnerId = ownerId,
                OwnerType = ConfigEntryOwnerType.Template,
                ValueType = "String",
                Values = ["default=localhost", "env:prod=sql.prod.internal"]
            },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("Database:ConnectionString");
        shellBuilder.GetOutput().ShouldContain("created");
        await client.Received(1).CreateConfigEntryHandlerAsync(
            Arg.Is<CreateConfigEntryRequest>(r =>
                r.Key == "Database:ConnectionString" &&
                r.OwnerId == ownerId &&
                r.OwnerType == ConfigEntryOwnerType.Template &&
                r.ValueType == "String" &&
                r.Values.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithValuesJson_ParsesJsonValues()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        var ownerId = Guid.CreateVersion7();
        client.CreateConfigEntryHandlerAsync(Arg.Any<CreateConfigEntryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ConfigEntryResponse
            {
                Id = Guid.CreateVersion7(),
                Key = "AppSetting",
                OwnerId = ownerId,
                OwnerType = ConfigEntryOwnerType.Project,
                ValueType = "String",
                Values = [new ScopedValue { Scopes = new Dictionary<string, string> { ["env"] = "prod" }, Value = "prodval" }],
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new CreateConfigEntryOptions
            {
                Key = "AppSetting",
                OwnerId = ownerId,
                OwnerType = ConfigEntryOwnerType.Project,
                ValueType = "String",
                ValuesJson = """[{"scopes":{"env":"prod"},"value":"prodval"}]"""
            },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).CreateConfigEntryHandlerAsync(
            Arg.Is<CreateConfigEntryRequest>(r => r.Values.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_MissingKey_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateConfigEntryOptions
            {
                OwnerId = Guid.CreateVersion7(),
                OwnerType = ConfigEntryOwnerType.Template,
                ValueType = "String"
            },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("--key");
        await client.DidNotReceive().CreateConfigEntryHandlerAsync(
            Arg.Any<CreateConfigEntryRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_MissingMultiple_ListsAllMissing()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateConfigEntryOptions(),
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("--key");
        output.ShouldContain("--owner-id");
        output.ShouldContain("--owner-type");
        output.ShouldContain("--value-type");
    }

    [Fact]
    public async Task HandleAsync_InvalidValueFormat_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateConfigEntryOptions
            {
                Key = "SomeKey",
                OwnerId = Guid.CreateVersion7(),
                OwnerType = ConfigEntryOwnerType.Template,
                ValueType = "String",
                Values = ["badformat"]
            },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Invalid scoped value format");
    }

    [Fact]
    public async Task HandleAsync_ApiValidationError_ShowsProblemDetails()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateConfigEntryHandlerAsync(Arg.Any<CreateConfigEntryRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<HttpValidationProblemDetails>(
                "Bad Request", 400, null, new Dictionary<string, IEnumerable<string>>(),
                new HttpValidationProblemDetails
                {
                    Status = 400,
                    Detail = "Validation failed.",
                    Errors = new Dictionary<string, ICollection<string>>
                    {
                        ["Key"] = ["Key is required."]
                    }
                }, null));

        var handler = CreateHandler(shellBuilder, client,
            new CreateConfigEntryOptions
            {
                Key = "",
                OwnerId = Guid.CreateVersion7(),
                OwnerType = ConfigEntryOwnerType.Template,
                ValueType = "String"
            },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Validation failed");
    }

    private static CreateConfigEntryHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        CreateConfigEntryOptions options,
        bool noInteractive) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}