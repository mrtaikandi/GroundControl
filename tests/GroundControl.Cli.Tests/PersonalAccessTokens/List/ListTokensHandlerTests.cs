using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.PersonalAccessTokens.List;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Tests.PersonalAccessTokens.List;

public sealed class ListTokensHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersTable()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListPatsHandlerAsync(Arg.Any<CancellationToken>())
            .Returns(new List<PatResponse>
            {
                CreateToken("CI token", "gc_ci_"),
                CreateToken("Dev token", "gc_dv_")
            });

        var handler = CreateHandler(shellBuilder, client, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).ListPatsHandlerAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_JsonOutput_RendersJsonArray()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListPatsHandlerAsync(Arg.Any<CancellationToken>())
            .Returns(new List<PatResponse>
            {
                CreateToken("CI token", "gc_ci_")
            });

        var handler = CreateHandler(shellBuilder, client, OutputFormat.Json);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("\"Name\"");
        output.ShouldContain("CI token");
    }

    private static ListTokensHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        OutputFormat outputFormat) =>
        new(
            shellBuilder.Build(),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);

    private static PatResponse CreateToken(string name, string prefix) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            TokenPrefix = prefix,
            IsRevoked = false,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow
        };
}