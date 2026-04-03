using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.PersonalAccessTokens.Revoke;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.PersonalAccessTokens.Revoke;

public sealed class RevokeTokenHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithYes_RevokesWithoutConfirmation()
    {
        // Arrange
        var tokenId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new RevokeTokenOptions { Id = tokenId, Yes = true });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("revoked");
        await client.Received(1).RevokePatHandlerAsync(tokenId, Arg.Any<CancellationToken>());
        await client.DidNotReceive().GetPatHandlerAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_RevokesWithoutConfirmation()
    {
        // Arrange
        var tokenId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new RevokeTokenOptions { Id = tokenId },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).RevokePatHandlerAsync(tokenId, Arg.Any<CancellationToken>());
        await client.DidNotReceive().GetPatHandlerAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NotFound_ShowsError()
    {
        // Arrange
        var tokenId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.RevokePatHandlerAsync(tokenId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Not Found", 404, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 404, Detail = "Token not found." }, null));

        var handler = CreateHandler(shellBuilder, client,
            new RevokeTokenOptions { Id = tokenId, Yes = true });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Token not found.");
    }

    [Fact]
    public async Task HandleAsync_GetPatFails_ShowsError()
    {
        // Arrange
        var tokenId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetPatHandlerAsync(tokenId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Not Found", 404, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 404, Detail = "Token not found." }, null));

        // Interactive mode with no --yes flag triggers a GetPat call for the confirmation prompt
        var handler = CreateHandler(shellBuilder, client,
            new RevokeTokenOptions { Id = tokenId, Yes = false },
            noInteractive: false);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Token not found.");
        await client.DidNotReceive().RevokePatHandlerAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private static RevokeTokenHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        RevokeTokenOptions options,
        bool noInteractive = false) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}