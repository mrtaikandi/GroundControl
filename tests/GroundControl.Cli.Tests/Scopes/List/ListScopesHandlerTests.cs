using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Scopes.List;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Tests.Scopes.List;

public sealed class ListScopesHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersTableWithColumns()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListScopesHandlerAsync(
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfScopeResponse
            {
                Data =
                [
                    CreateScope("Environment", ["dev", "staging", "prod"]),
                    CreateScope("Region", ["us-east", "eu-west"])
                ],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Id");
        output.ShouldContain("Dimension");
        output.ShouldContain("Values");
        output.ShouldContain("Environment");
        output.ShouldContain("3");
        output.ShouldContain("Region");
        output.ShouldContain("2");
    }

    [Fact]
    public async Task HandleAsync_JsonOutput_RendersJsonArray()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListScopesHandlerAsync(
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfScopeResponse
            {
                Data = [CreateScope("Environment", ["dev"])],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, OutputFormat.Json);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("\"Dimension\"");
        output.ShouldContain("Environment");
    }

    private static ListScopesHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        OutputFormat outputFormat) =>
        new(
            shellBuilder.Build(),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);

    private static ScopeResponse CreateScope(string dimension, string[] values) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Dimension = dimension,
            AllowedValues = values,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
}