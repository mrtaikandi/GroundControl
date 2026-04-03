using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Templates.Update;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Templates.Update;

public sealed class UpdateTemplateHandlerTests
{
    [Fact]
    public async Task HandleAsync_UpdatesTemplate()
    {
        // Arrange
        var templateId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.UpdateTemplateHandlerAsync(templateId, Arg.Any<UpdateTemplateRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TemplateResponse
            {
                Id = templateId,
                Name = "Updated Template",
                Version = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateTemplateOptions { Id = templateId, Name = "Updated Template", Version = 1 });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("Updated Template");
        shellBuilder.GetOutput().ShouldContain("updated");
    }

    [Fact]
    public async Task HandleAsync_NoVersion_FetchesCurrentFirst()
    {
        // Arrange
        var templateId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetTemplateHandlerAsync(templateId, Arg.Any<CancellationToken>())
            .Returns(new TemplateResponse
            {
                Id = templateId,
                Name = "Current",
                Version = 5,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        client.UpdateTemplateHandlerAsync(templateId, Arg.Any<UpdateTemplateRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TemplateResponse
            {
                Id = templateId,
                Name = "Updated",
                Version = 6,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateTemplateOptions { Id = templateId, Name = "Updated" });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).GetTemplateHandlerAsync(templateId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Conflict_NonInteractive_ShowsDiffAndFails()
    {
        // Arrange
        var templateId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        client.UpdateTemplateHandlerAsync(templateId, Arg.Any<UpdateTemplateRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Conflict", 409, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 409, Detail = "Version conflict." }, null));

        client.GetTemplateHandlerAsync(templateId, Arg.Any<CancellationToken>())
            .Returns(new TemplateResponse
            {
                Id = templateId,
                Name = "Server Name",
                Description = "Server Desc",
                Version = 10,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateTemplateOptions { Id = templateId, Name = "My Name", Version = 5 },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Version conflict");
        output.ShouldContain("My Name");
        output.ShouldContain("Server Name");
        output.ShouldContain("10");
    }

    private static UpdateTemplateHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        UpdateTemplateOptions options,
        bool noInteractive = false) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}