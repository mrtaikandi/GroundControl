using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Projects.Create;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Projects.Create;

public sealed class CreateProjectHandlerTests
{
    [Fact]
    public async Task HandleAsync_AllOptions_CreatesProject()
    {
        // Arrange
        var templateId1 = Guid.CreateVersion7();
        var templateId2 = Guid.CreateVersion7();
        var groupId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateProjectHandlerAsync(Arg.Any<CreateProjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProjectResponse
            {
                Id = Guid.CreateVersion7(),
                Name = "MyProject",
                GroupId = groupId,
                TemplateIds = [templateId1, templateId2],
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new CreateProjectOptions
            {
                Name = "MyProject",
                GroupId = groupId,
                TemplateIds = $"{templateId1},{templateId2}",
                Description = "A test project"
            },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("MyProject");
        shellBuilder.GetOutput().ShouldContain("created");
        await client.Received(1).CreateProjectHandlerAsync(
            Arg.Is<CreateProjectRequest>(r =>
                r.Name == "MyProject" &&
                r.GroupId == groupId &&
                r.TemplateIds!.Count == 2 &&
                r.Description == "A test project"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NameOnly_CreatesProjectWithoutGroupOrTemplates()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateProjectHandlerAsync(Arg.Any<CreateProjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProjectResponse
            {
                Id = Guid.CreateVersion7(),
                Name = "SimpleProject",
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new CreateProjectOptions { Name = "SimpleProject" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("SimpleProject");
        await client.Received(1).CreateProjectHandlerAsync(
            Arg.Is<CreateProjectRequest>(r =>
                r.Name == "SimpleProject" &&
                r.GroupId == null &&
                r.TemplateIds == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_MissingName_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateProjectOptions(),
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("--name");
        await client.DidNotReceive().CreateProjectHandlerAsync(
            Arg.Any<CreateProjectRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ApiValidationError_ShowsProblemDetails()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateProjectHandlerAsync(Arg.Any<CreateProjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<HttpValidationProblemDetails>(
                "Bad Request", 400, null, new Dictionary<string, IEnumerable<string>>(),
                new HttpValidationProblemDetails
                {
                    Status = 400,
                    Detail = "Validation failed.",
                    Errors = new Dictionary<string, ICollection<string>>
                    {
                        ["Name"] = ["Name is required."]
                    }
                }, null));

        var handler = CreateHandler(shellBuilder, client,
            new CreateProjectOptions { Name = "" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Validation failed");
    }

    [Fact]
    public async Task HandleAsync_ApiError_ShowsProblemDetails()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateProjectHandlerAsync(Arg.Any<CreateProjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Conflict", 409, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 409, Detail = "A project with this name already exists." }, null));

        var handler = CreateHandler(shellBuilder, client,
            new CreateProjectOptions { Name = "DuplicateProject" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("already exists");
    }

    private static CreateProjectHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        CreateProjectOptions options,
        bool noInteractive) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}