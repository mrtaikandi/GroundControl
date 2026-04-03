using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Projects.List;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Tests.Projects.List;

public sealed class ListProjectsHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersTableWithColumns()
    {
        // Arrange
        var groupId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListProjectsHandlerAsync(
                Arg.Any<Guid?>(), Arg.Any<string?>(),
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfProjectResponse
            {
                Data =
                [
                    CreateProject("Alpha", groupId, 3),
                    CreateProject("Beta", null, 0)
                ],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).ListProjectsHandlerAsync(
            Arg.Any<Guid?>(), Arg.Any<string?>(),
            Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_JsonOutput_RendersJsonArray()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListProjectsHandlerAsync(
                Arg.Any<Guid?>(), Arg.Any<string?>(),
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfProjectResponse
            {
                Data = [CreateProject("Alpha", null, 1)],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, OutputFormat.Json);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("\"Name\"");
        output.ShouldContain("Alpha");
    }

    private static ListProjectsHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        OutputFormat outputFormat) =>
        new(
            shellBuilder.Build(),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);

    private static ProjectResponse CreateProject(string name, Guid? groupId, int templateCount)
    {
        var templateIds = new List<Guid>();
        for (var i = 0; i < templateCount; i++)
        {
            templateIds.Add(Guid.CreateVersion7());
        }

        return new ProjectResponse
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            GroupId = groupId,
            TemplateIds = templateIds,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}