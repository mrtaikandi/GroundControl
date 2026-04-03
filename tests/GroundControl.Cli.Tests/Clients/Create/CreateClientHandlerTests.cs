using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Clients.Create;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Clients.Create;

public sealed class CreateClientHandlerTests
{
    [Fact]
    public async Task HandleAsync_AllOptions_CreatesClientAndDisplaysSecret()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateClientHandlerAsync(projectId, Arg.Any<CreateClientRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateClientResponse
            {
                Id = clientId,
                ProjectId = projectId,
                Name = "MyClient",
                ClientSecret = "secret-abc-123",
                IsActive = true,
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new CreateClientOptions
            {
                ProjectId = projectId,
                Name = "MyClient",
                Scopes = "env=prod,region=us"
            },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("MyClient");
        output.ShouldContain("created");
        output.ShouldContain("secret-abc-123");
        output.ShouldContain("shown only once");
        await client.Received(1).CreateClientHandlerAsync(
            projectId,
            Arg.Is<CreateClientRequest>(r =>
                r.Name == "MyClient" &&
                r.Scopes!.Count == 2 &&
                r.Scopes["env"] == "prod" &&
                r.Scopes["region"] == "us"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NameOnly_CreatesClientWithoutScopes()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateClientHandlerAsync(projectId, Arg.Any<CreateClientRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateClientResponse
            {
                Id = Guid.CreateVersion7(),
                ProjectId = projectId,
                Name = "SimpleClient",
                ClientSecret = "secret-xyz",
                IsActive = true,
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new CreateClientOptions { ProjectId = projectId, Name = "SimpleClient" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("SimpleClient");
        output.ShouldContain("secret-xyz");
        await client.Received(1).CreateClientHandlerAsync(
            projectId,
            Arg.Is<CreateClientRequest>(r =>
                r.Name == "SimpleClient" &&
                r.Scopes == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_MissingName_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateClientOptions { ProjectId = Guid.CreateVersion7() },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("--name");
        await client.DidNotReceive().CreateClientHandlerAsync(
            Arg.Any<Guid>(), Arg.Any<CreateClientRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ApiValidationError_ShowsProblemDetails()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateClientHandlerAsync(projectId, Arg.Any<CreateClientRequest>(), Arg.Any<CancellationToken>())
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
            new CreateClientOptions { ProjectId = projectId, Name = "" },
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
        var projectId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateClientHandlerAsync(projectId, Arg.Any<CreateClientRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Conflict", 409, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 409, Detail = "A client with this name already exists." }, null));

        var handler = CreateHandler(shellBuilder, client,
            new CreateClientOptions { ProjectId = projectId, Name = "DuplicateClient" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("already exists");
    }

    private static CreateClientHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        CreateClientOptions options,
        bool noInteractive) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}