using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Clients.Update;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Clients.Update;

public sealed class UpdateClientHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithVersion_UpdatesClient()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.UpdateClientHandlerAsync(projectId, clientId, Arg.Any<UpdateClientRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ClientResponse
            {
                Id = clientId,
                ProjectId = projectId,
                Name = "UpdatedClient",
                IsActive = true,
                Version = 4,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateClientOptions
            {
                ProjectId = projectId,
                Id = clientId,
                Name = "UpdatedClient",
                Version = 3
            });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("UpdatedClient");
        output.ShouldContain("updated");
        await client.Received(1).UpdateClientHandlerAsync(
            projectId, clientId,
            Arg.Is<UpdateClientRequest>(r => r.Name == "UpdatedClient"),
            Arg.Any<CancellationToken>());
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
                Version = 5,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        client.UpdateClientHandlerAsync(projectId, clientId, Arg.Any<UpdateClientRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ClientResponse
            {
                Id = clientId,
                ProjectId = projectId,
                Name = "RenamedClient",
                IsActive = true,
                Version = 6,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateClientOptions
            {
                ProjectId = projectId,
                Id = clientId,
                Name = "RenamedClient"
            });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).GetClientHandlerAsync(projectId, clientId, Arg.Any<CancellationToken>());
        await client.Received(1).UpdateClientHandlerAsync(
            projectId, clientId, Arg.Any<UpdateClientRequest>(), Arg.Any<CancellationToken>());
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
            new UpdateClientOptions { ProjectId = projectId, Id = clientId, Name = "NewName" });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Client not found.");
    }

    [Fact]
    public async Task HandleAsync_ApiValidationError_ShowsProblemDetails()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.UpdateClientHandlerAsync(projectId, clientId, Arg.Any<UpdateClientRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<HttpValidationProblemDetails>(
                "Bad Request", 400, null, new Dictionary<string, IEnumerable<string>>(),
                new HttpValidationProblemDetails
                {
                    Status = 400,
                    Detail = "Validation failed.",
                    Errors = new Dictionary<string, ICollection<string>>
                    {
                        ["Name"] = ["Name cannot be empty."]
                    }
                }, null));

        var handler = CreateHandler(shellBuilder, client,
            new UpdateClientOptions { ProjectId = projectId, Id = clientId, Name = "", Version = 1 });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Validation failed");
    }

    private static UpdateClientHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        UpdateClientOptions options,
        bool noInteractive = true) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}