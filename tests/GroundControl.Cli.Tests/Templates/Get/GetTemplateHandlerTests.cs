using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Templates.Get;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Templates.Get;

public sealed class GetTemplateHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersTemplateDetail()
    {
        // Arrange
        var templateId = Guid.CreateVersion7();
        var groupId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetTemplateHandlerAsync(templateId, Arg.Any<CancellationToken>())
            .Returns(new TemplateResponse
            {
                Id = templateId,
                Name = "My Template",
                Description = "A test template",
                GroupId = groupId,
                Version = 3,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client, templateId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("My Template");
        output.ShouldContain("A test template");
        output.ShouldContain(templateId.ToString());
        output.ShouldContain(groupId.ToString());
    }

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

        var handler = CreateHandler(shellBuilder, client, templateId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Template not found.");
    }

    private static GetTemplateHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        Guid id,
        OutputFormat outputFormat) =>
        new(
            shellBuilder.Build(),
            Options.Create(new GetTemplateOptions { Id = id }),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);
}