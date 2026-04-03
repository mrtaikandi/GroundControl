using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Clients.Get;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Clients.Get;

public sealed class GetClientHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersClientDetail()
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
                Scopes = new Dictionary<string, string> { ["env"] = "prod" },
                Version = 3,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client, projectId, clientId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("MyClient");
        output.ShouldContain(clientId.ToString());
        output.ShouldContain(projectId.ToString());
        output.ShouldContain("env=prod");
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

        var handler = CreateHandler(shellBuilder, client, projectId, clientId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Client not found.");
    }

    private static GetClientHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        Guid projectId,
        Guid id,
        OutputFormat outputFormat) =>
        new(
            shellBuilder.Build(),
            Options.Create(new GetClientOptions { ProjectId = projectId, Id = id }),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);
}