using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Templates.Delete;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Templates.Delete;

public sealed class DeleteTemplateHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithYes_DeletesWithoutConfirmation()
    {
        // Arrange
        var templateId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetTemplateHandlerAsync(templateId, Arg.Any<CancellationToken>())
            .Returns(new TemplateResponse
            {
                Id = templateId,
                Name = "To Delete",
                Version = 3,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new DeleteTemplateOptions { Id = templateId, Version = 3, Yes = true });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("deleted");
        await client.Received(1).DeleteTemplateHandlerAsync(templateId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_DeletesWithoutConfirmation()
    {
        // Arrange
        var templateId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetTemplateHandlerAsync(templateId, Arg.Any<CancellationToken>())
            .Returns(new TemplateResponse
            {
                Id = templateId,
                Name = "To Delete",
                Version = 3,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new DeleteTemplateOptions { Id = templateId, Version = 3 },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).DeleteTemplateHandlerAsync(templateId, Arg.Any<CancellationToken>());
    }

    // Interactive confirmation declined test is not possible with MockShellBuilder because
    // Spectre.Console's ConfirmAsync requires real console input (ReadKey).
    // The decline flow is covered by the acceptance criteria at the integration level.

    [Fact]
    public async Task HandleAsync_NotFound_ShowsError()
    {
        // Arrange
        var templateId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetTemplateHandlerAsync(templateId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Not Found", 404, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 404, Detail = "Template not found." }, null));

        var handler = CreateHandler(shellBuilder, client,
            new DeleteTemplateOptions { Id = templateId });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Template not found.");
    }

    private static DeleteTemplateHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        DeleteTemplateOptions options,
        bool noInteractive = false) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}