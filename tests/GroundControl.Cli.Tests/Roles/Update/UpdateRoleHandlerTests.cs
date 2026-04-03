using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Roles.Update;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Roles.Update;

public sealed class UpdateRoleHandlerTests
{
    [Fact]
    public async Task HandleAsync_UpdatesRole()
    {
        // Arrange
        var roleId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.UpdateRoleHandlerAsync(roleId, Arg.Any<UpdateRoleRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RoleResponse
            {
                Id = roleId,
                Name = "Editor-Updated",
                Permissions = ["scopes:read", "scopes:write"],
                Version = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateRoleOptions { Id = roleId, Name = "Editor-Updated", Version = 1 });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("updated");
    }

    [Fact]
    public async Task HandleAsync_NoVersion_FetchesCurrentFirst()
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
                Permissions = ["scopes:read"],
                Version = 5,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        client.UpdateRoleHandlerAsync(roleId, Arg.Any<UpdateRoleRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RoleResponse
            {
                Id = roleId,
                Name = "Admin-Updated",
                Permissions = ["scopes:read"],
                Version = 6,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateRoleOptions { Id = roleId, Name = "Admin-Updated" });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).GetRoleHandlerAsync(roleId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_UpdatePermissions_SendsNewPermissions()
    {
        // Arrange
        var roleId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.UpdateRoleHandlerAsync(roleId, Arg.Any<UpdateRoleRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RoleResponse
            {
                Id = roleId,
                Name = "Editor",
                Permissions = ["scopes:read", "scopes:write", "groups:read"],
                Version = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateRoleOptions { Id = roleId, Permissions = "scopes:read,scopes:write,groups:read", Version = 1 });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).UpdateRoleHandlerAsync(
            roleId,
            Arg.Is<UpdateRoleRequest>(r =>
                r.Permissions.Count == 3 &&
                r.Permissions.Contains("scopes:read") &&
                r.Permissions.Contains("scopes:write") &&
                r.Permissions.Contains("groups:read")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Conflict_NonInteractive_ShowsDiffAndFails()
    {
        // Arrange
        var roleId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        client.UpdateRoleHandlerAsync(roleId, Arg.Any<UpdateRoleRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Conflict", 409, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 409, Detail = "Version conflict." }, null));

        client.GetRoleHandlerAsync(roleId, Arg.Any<CancellationToken>())
            .Returns(new RoleResponse
            {
                Id = roleId,
                Name = "Admin-Server",
                Permissions = ["scopes:read"],
                Version = 10,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new UpdateRoleOptions { Id = roleId, Name = "Admin-Local", Version = 5 },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Version conflict");
        output.ShouldContain("Admin-Local");
        output.ShouldContain("Admin-Server");
        output.ShouldContain("10");
    }

    private static UpdateRoleHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        UpdateRoleOptions options,
        bool noInteractive = false) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}