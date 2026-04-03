using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Groups.Get;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Groups.Get;

public sealed class GetGroupHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersGroupDetail()
    {
        // Arrange
        var groupId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetGroupHandlerAsync(groupId, Arg.Any<CancellationToken>())
            .Returns(new GroupResponse
            {
                Id = groupId,
                Name = "My Group",
                Description = "A test group",
                Version = 3,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client, groupId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("My Group");
        output.ShouldContain("A test group");
        output.ShouldContain(groupId.ToString());
    }

    [Fact]
    public async Task HandleAsync_NotFound_ShowsError()
    {
        // Arrange
        var groupId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetGroupHandlerAsync(groupId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Not Found", 404, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 404, Detail = "Group not found." }, null));

        var handler = CreateHandler(shellBuilder, client, groupId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Group not found.");
    }

    private static GetGroupHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        Guid id,
        OutputFormat outputFormat) =>
        new(
            shellBuilder.Build(),
            Options.Create(new GetGroupOptions { Id = id }),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);
}