using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Clients.Delete;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Clients.Delete;

public sealed class DeleteClientHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithYes_DeletesWithoutConfirmation()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetClientHandlerAsync(projectId, clientId, Arg.Any<CancellationToken>())
            .Returns(new ClientResponse
            {
                Id = clientId,
                ProjectId = projectId,
                Name = "MyClient",
                IsActive = true,
                Version = 3,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new DeleteClientOptions { ProjectId = projectId, Id = clientId, Version = 3, Yes = true });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("deleted");
        await client.Received(1).DeleteClientHandlerAsync(projectId, clientId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_DeletesWithoutConfirmation()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetClientHandlerAsync(projectId, clientId, Arg.Any<CancellationToken>())
            .Returns(new ClientResponse
            {
                Id = clientId,
                ProjectId = projectId,
                Name = "MyClient",
                IsActive = true,
                Version = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new DeleteClientOptions { ProjectId = projectId, Id = clientId, Version = 2 },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).DeleteClientHandlerAsync(projectId, clientId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NotFound_ShowsError()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetClientHandlerAsync(projectId, clientId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Not Found", 404, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 404, Detail = "Client not found." }, null));

        var handler = CreateHandler(shellBuilder, client,
            new DeleteClientOptions { ProjectId = projectId, Id = clientId });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Client not found.");
    }

    [Fact]
    public async Task HandleAsync_NoVersion_FetchesCurrentFirst()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetClientHandlerAsync(projectId, clientId, Arg.Any<CancellationToken>())
            .Returns(new ClientResponse
            {
                Id = clientId,
                ProjectId = projectId,
                Name = "MyClient",
                IsActive = true,
                Version = 7,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new DeleteClientOptions { ProjectId = projectId, Id = clientId, Yes = true });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).GetClientHandlerAsync(projectId, clientId, Arg.Any<CancellationToken>());
        await client.Received(1).DeleteClientHandlerAsync(projectId, clientId, Arg.Any<CancellationToken>());
    }

    private static DeleteClientHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        DeleteClientOptions options,
        bool noInteractive = false) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}