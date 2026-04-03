using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Clients.List;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Tests.Clients.List;

public sealed class ListClientsHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersTableWithColumns()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListClientsHandlerAsync(
                projectId,
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfClientResponse
            {
                Data =
                [
                    CreateClient("Alpha", true, 3),
                    CreateClient("Beta", false, 0)
                ],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, projectId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).ListClientsHandlerAsync(
            projectId,
            Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_JsonOutput_RendersJsonArray()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListClientsHandlerAsync(
                projectId,
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfClientResponse
            {
                Data = [CreateClient("Alpha", true, 1)],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, projectId, OutputFormat.Json);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("\"Name\"");
        output.ShouldContain("Alpha");
    }

    private static ListClientsHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        Guid projectId,
        OutputFormat outputFormat) =>
        new(
            shellBuilder.Build(),
            Options.Create(new ListClientsOptions { ProjectId = projectId }),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);

    private static ClientResponse CreateClient(string name, bool isActive, int scopeCount)
    {
        var scopes = new Dictionary<string, string>();
        for (var i = 0; i < scopeCount; i++)
        {
            scopes[$"dim{i}"] = $"val{i}";
        }

        return new ClientResponse
        {
            Id = Guid.CreateVersion7(),
            ProjectId = Guid.CreateVersion7(),
            Name = name,
            IsActive = isActive,
            Scopes = scopes,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}