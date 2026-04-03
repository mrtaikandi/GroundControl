using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Groups.List;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Tests.Groups.List;

public sealed class ListGroupsHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersTableWithColumns()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListGroupsHandlerAsync(
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfGroupResponse
            {
                Data = [CreateGroup("Group A", "First group"), CreateGroup("Group B", "Second group")],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Id");
        output.ShouldContain("Name");
        output.ShouldContain("Description");
        output.ShouldContain("Group A");
        output.ShouldContain("First group");
        output.ShouldContain("Group B");
        output.ShouldContain("Second group");
    }

    [Fact]
    public async Task HandleAsync_JsonOutput_RendersJsonArray()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListGroupsHandlerAsync(
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfGroupResponse
            {
                Data = [CreateGroup("Group A")],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, OutputFormat.Json);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("\"Name\"");
        output.ShouldContain("Group A");
    }

    private static ListGroupsHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        OutputFormat outputFormat) =>
        new(
            shellBuilder.Build(),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);

    private static GroupResponse CreateGroup(string name, string? description = null) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Description = description,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
}