using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Roles.Get;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Roles.Get;

public sealed class GetRoleHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersRoleDetail()
    {
        // Arrange
        var roleId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetRoleHandlerAsync(roleId, Arg.Any<CancellationToken>())
            .Returns(new RoleResponse
            {
                Id = roleId,
                Name = "Admin",
                Description = "Full access",
                Permissions = ["scopes:read", "scopes:write"],
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client, new GetRoleOptions { Id = roleId });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Admin");
        output.ShouldContain("Full access");
        output.ShouldContain("scopes:read");
        output.ShouldContain("scopes:write");
        output.ShouldContain(roleId.ToString());
    }

    [Fact]
    public async Task HandleAsync_NotFound_ShowsError()
    {
        // Arrange
        var roleId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetRoleHandlerAsync(roleId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Not Found", 404, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 404, Detail = "Role not found." }, null));

        var handler = CreateHandler(shellBuilder, client, new GetRoleOptions { Id = roleId });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Role not found.");
    }

    private static GetRoleHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        GetRoleOptions options) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions()),
            client);
}