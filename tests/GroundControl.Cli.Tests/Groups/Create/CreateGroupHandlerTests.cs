using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Groups.Create;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Groups.Create;

public sealed class CreateGroupHandlerTests
{
    [Fact]
    public async Task HandleAsync_AllOptions_CreatesGroup()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateGroupHandlerAsync(Arg.Any<CreateGroupRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GroupResponse
            {
                Id = Guid.CreateVersion7(),
                Name = "New Group",
                Description = "A description",
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new CreateGroupOptions { Name = "New Group", Description = "A description" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("New Group");
        shellBuilder.GetOutput().ShouldContain("created");
        await client.Received(1).CreateGroupHandlerAsync(
            Arg.Is<CreateGroupRequest>(r => r.Name == "New Group" && r.Description == "A description"),
            Arg.Any<CancellationToken>());
    }

    // Interactive create prompt tests are not possible with MockShellBuilder because
    // Spectre.Console's PromptForStringAsync requires real console input (ReadKey).
    // The interactive flow is covered by the acceptance criteria at the integration level.

    [Fact]
    public async Task HandleAsync_NonInteractive_MissingName_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateGroupOptions(),
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("--name");
        await client.DidNotReceive().CreateGroupHandlerAsync(
            Arg.Any<CreateGroupRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ApiValidationError_ShowsProblemDetails()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateGroupHandlerAsync(Arg.Any<CreateGroupRequest>(), Arg.Any<CancellationToken>())
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
            new CreateGroupOptions { Name = "" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Validation failed");
    }

    private static CreateGroupHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        CreateGroupOptions options,
        bool noInteractive) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}