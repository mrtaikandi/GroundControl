using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.PersonalAccessTokens.Get;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.PersonalAccessTokens.Get;

public sealed class GetTokenHandlerTests
{
    [Fact]
    public async Task HandleAsync_ExistingToken_RendersDetail()
    {
        // Arrange
        var tokenId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetPatHandlerAsync(tokenId, Arg.Any<CancellationToken>())
            .Returns(new PatResponse
            {
                Id = tokenId,
                Name = "CI token",
                TokenPrefix = "gc_ci_",
                IsRevoked = false,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                CreatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client, new GetTokenOptions { Id = tokenId });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("CI token");
        output.ShouldContain("gc_ci_");
    }

    [Fact]
    public async Task HandleAsync_NotFound_ShowsError()
    {
        // Arrange
        var tokenId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetPatHandlerAsync(tokenId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Not Found", 404, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 404, Detail = "Token not found." }, null));

        var handler = CreateHandler(shellBuilder, client, new GetTokenOptions { Id = tokenId });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Token not found.");
    }

    private static GetTokenHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        GetTokenOptions options) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions()),
            client);
}