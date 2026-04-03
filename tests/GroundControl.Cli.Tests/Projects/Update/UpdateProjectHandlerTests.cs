using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Projects.Update;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Projects.Update;

public sealed class UpdateProjectHandlerTests
{
    [Fact]
    public async Task HandleAsync_UpdatesProject()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.UpdateProjectHandlerAsync(projectId, Arg.Any<UpdateProjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProjectResponse
            {
                Id = projectId,
                Name = "UpdatedProject",
                Version = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateProjectOptions { Id = projectId, Name = "UpdatedProject", Version = 1 });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("updated");
    }

    [Fact]
    public async Task HandleAsync_NoVersion_FetchesCurrentFirst()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetProjectHandlerAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(new ProjectResponse
            {
                Id = projectId,
                Name = "MyProject",
                Version = 5,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        client.UpdateProjectHandlerAsync(projectId, Arg.Any<UpdateProjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProjectResponse
            {
                Id = projectId,
                Name = "RenamedProject",
                Version = 6,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateProjectOptions { Id = projectId, Name = "RenamedProject" });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).GetProjectHandlerAsync(projectId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Conflict_NonInteractive_ShowsDiffAndFails()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        client.UpdateProjectHandlerAsync(projectId, Arg.Any<UpdateProjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Conflict", 409, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 409, Detail = "Version conflict." }, null));

        client.GetProjectHandlerAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(new ProjectResponse
            {
                Id = projectId,
                Name = "ServerName",
                Version = 10,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateProjectOptions { Id = projectId, Name = "LocalName", Version = 5 },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Version conflict");
        output.ShouldContain("LocalName");
        output.ShouldContain("ServerName");
        output.ShouldContain("10");
    }

    [Fact]
    public async Task HandleAsync_WithTemplateIds_UpdatesTemplates()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var templateId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.UpdateProjectHandlerAsync(projectId, Arg.Any<UpdateProjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProjectResponse
            {
                Id = projectId,
                Name = "MyProject",
                TemplateIds = [templateId],
                Version = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateProjectOptions { Id = projectId, TemplateIds = templateId.ToString(), Version = 1 });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).UpdateProjectHandlerAsync(
            projectId,
            Arg.Is<UpdateProjectRequest>(r => r.TemplateIds!.Count == 1),
            Arg.Any<CancellationToken>());
    }

    private static UpdateProjectHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        UpdateProjectOptions options,
        bool noInteractive = false) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}