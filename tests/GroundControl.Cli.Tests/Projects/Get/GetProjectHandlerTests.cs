using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Projects.Get;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Projects.Get;

public sealed class GetProjectHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersProjectDetail()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var groupId = Guid.CreateVersion7();
        var templateId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetProjectHandlerAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(new ProjectResponse
            {
                Id = projectId,
                Name = "MyProject",
                Description = "A test project",
                GroupId = groupId,
                TemplateIds = [templateId],
                Version = 3,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client, projectId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("MyProject");
        output.ShouldContain("A test project");
        output.ShouldContain(projectId.ToString());
        output.ShouldContain(groupId.ToString());
        output.ShouldContain(templateId.ToString());
    }

    [Fact]
    public async Task HandleAsync_NotFound_ShowsError()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetProjectHandlerAsync(projectId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Not Found", 404, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 404, Detail = "Project not found." }, null));

        var handler = CreateHandler(shellBuilder, client, projectId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Project not found.");
    }

    private static GetProjectHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        Guid id,
        OutputFormat outputFormat) =>
        new(
            shellBuilder.Build(),
            Options.Create(new GetProjectOptions { Id = id }),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);
}