using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Scopes.Update;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Scopes.Update;

public sealed class UpdateScopeHandlerTests
{
    [Fact]
    public async Task HandleAsync_UpdatesScope()
    {
        // Arrange
        var scopeId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.UpdateScopeHandlerAsync(scopeId, Arg.Any<UpdateScopeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ScopeResponse
            {
                Id = scopeId,
                Dimension = "Environment",
                AllowedValues = ["dev", "staging", "prod", "canary"],
                Version = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateScopeOptions { Id = scopeId, Values = "dev,staging,prod,canary", Version = 1 });

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
        var scopeId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetScopeHandlerAsync(scopeId, Arg.Any<CancellationToken>())
            .Returns(new ScopeResponse
            {
                Id = scopeId,
                Dimension = "Environment",
                AllowedValues = ["dev"],
                Version = 5,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        client.UpdateScopeHandlerAsync(scopeId, Arg.Any<UpdateScopeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ScopeResponse
            {
                Id = scopeId,
                Dimension = "Environment",
                AllowedValues = ["dev", "staging"],
                Version = 6,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateScopeOptions { Id = scopeId, Values = "dev,staging" });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).GetScopeHandlerAsync(scopeId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Conflict_NonInteractive_ShowsDiffAndFails()
    {
        // Arrange
        var scopeId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        client.UpdateScopeHandlerAsync(scopeId, Arg.Any<UpdateScopeRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Conflict", 409, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 409, Detail = "Version conflict." }, null));

        client.GetScopeHandlerAsync(scopeId, Arg.Any<CancellationToken>())
            .Returns(new ScopeResponse
            {
                Id = scopeId,
                Dimension = "Environment",
                AllowedValues = ["dev", "staging", "prod"],
                Version = 10,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateScopeOptions { Id = scopeId, Values = "dev,staging", Version = 5 },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Version conflict");
        output.ShouldContain("dev, staging");
        output.ShouldContain("dev, staging, prod");
        output.ShouldContain("10");
    }

    private static UpdateScopeHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        UpdateScopeOptions options,
        bool noInteractive = false) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}