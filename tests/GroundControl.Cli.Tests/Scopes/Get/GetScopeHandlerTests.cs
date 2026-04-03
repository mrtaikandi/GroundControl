using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Scopes.Get;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Scopes.Get;

public sealed class GetScopeHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersScopeDetail()
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
                AllowedValues = ["dev", "staging", "prod"],
                Description = "Deployment environment",
                Version = 3,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client, scopeId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Environment");
        output.ShouldContain("dev, staging, prod");
        output.ShouldContain("Deployment environment");
        output.ShouldContain(scopeId.ToString());
    }

    [Fact]
    public async Task HandleAsync_NotFound_ShowsError()
    {
        // Arrange
        var scopeId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetScopeHandlerAsync(scopeId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Not Found", 404, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 404, Detail = "Scope not found." }, null));

        var handler = CreateHandler(shellBuilder, client, scopeId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Scope not found.");
    }

    private static GetScopeHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        Guid id,
        OutputFormat outputFormat) =>
        new(
            shellBuilder.Build(),
            Options.Create(new GetScopeOptions { Id = id }),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);
}