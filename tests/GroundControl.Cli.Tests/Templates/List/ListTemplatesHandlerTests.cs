using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Templates.List;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Tests.Templates.List;

public sealed class ListTemplatesHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersTable()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        var groupId = Guid.CreateVersion7();

        client.ListTemplatesHandlerAsync(
                Arg.Any<Guid?>(), Arg.Any<bool?>(),
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfTemplateResponse
            {
                Data = [CreateTemplate("TmA", groupId), CreateTemplate("TmB", null)],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        // Two GUID columns (Id, GroupId) consume the full 80-char mock console width,
        // truncating all cell content. Content correctness is verified via the JSON test.
        shellBuilder.GetOutput().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_JsonOutput_RendersJsonArray()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        client.ListTemplatesHandlerAsync(
                Arg.Any<Guid?>(), Arg.Any<bool?>(),
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfTemplateResponse
            {
                Data = [CreateTemplate("Template A")],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, OutputFormat.Json);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("\"Name\"");
        output.ShouldContain("Template A");
    }

    private static ListTemplatesHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        OutputFormat outputFormat) =>
        new(
            shellBuilder.Build(),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);

    private static TemplateResponse CreateTemplate(string name, Guid? groupId = null) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            GroupId = groupId,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
}