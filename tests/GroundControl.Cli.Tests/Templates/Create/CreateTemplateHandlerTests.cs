using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Templates.Create;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Templates.Create;

public sealed class CreateTemplateHandlerTests
{
    [Fact]
    public async Task HandleAsync_AllOptions_CreatesTemplate()
    {
        // Arrange
        var groupId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateTemplateHandlerAsync(Arg.Any<CreateTemplateRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TemplateResponse
            {
                Id = Guid.CreateVersion7(),
                Name = "New Template",
                Description = "A description",
                GroupId = groupId,
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new CreateTemplateOptions { Name = "New Template", Description = "A description", GroupId = groupId },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("New Template");
        shellBuilder.GetOutput().ShouldContain("created");
        await client.Received(1).CreateTemplateHandlerAsync(
            Arg.Is<CreateTemplateRequest>(r =>
                r.Name == "New Template" &&
                r.Description == "A description" &&
                r.GroupId == groupId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithoutGroupId_CreatesTemplate()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateTemplateHandlerAsync(Arg.Any<CreateTemplateRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TemplateResponse
            {
                Id = Guid.CreateVersion7(),
                Name = "Global Template",
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new CreateTemplateOptions { Name = "Global Template" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("Global Template");
        await client.Received(1).CreateTemplateHandlerAsync(
            Arg.Is<CreateTemplateRequest>(r => r.Name == "Global Template" && r.GroupId == null),
            Arg.Any<CancellationToken>());
    }

    // Interactive create prompt tests are not possible with MockShellBuilder because
    // Spectre.Console's PromptForStringAsync and PromptForSelectionAsync require real
    // console input (ReadKey). The interactive flow and group selection fallback are
    // covered by the acceptance criteria at the integration level.

    [Fact]
    public async Task HandleAsync_NonInteractive_MissingName_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateTemplateOptions(),
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("--name");
        await client.DidNotReceive().CreateTemplateHandlerAsync(
            Arg.Any<CreateTemplateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ApiValidationError_ShowsProblemDetails()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateTemplateHandlerAsync(Arg.Any<CreateTemplateRequest>(), Arg.Any<CancellationToken>())
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
            new CreateTemplateOptions { Name = "" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Validation failed");
    }

    private static CreateTemplateHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        CreateTemplateOptions options,
        bool noInteractive) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}