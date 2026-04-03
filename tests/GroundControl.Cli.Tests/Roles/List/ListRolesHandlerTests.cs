using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Roles.List;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Tests.Roles.List;

public sealed class ListRolesHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersTableWithColumns()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListRolesHandlerAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RoleResponse>
            {
                CreateRole("Admin", ["scopes:read", "scopes:write"]),
                CreateRole("Viewer", ["scopes:read"])
            });

        var handler = CreateHandler(shellBuilder, client, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Id");
        output.ShouldContain("Name");
        output.ShouldContain("PermissionCount");
        output.ShouldContain("Admin");
        output.ShouldContain("Viewer");
        output.ShouldContain("2");
        output.ShouldContain("1");
    }

    [Fact]
    public async Task HandleAsync_JsonOutput_RendersJsonArray()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListRolesHandlerAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RoleResponse>
            {
                CreateRole("Admin", ["scopes:read"])
            });

        var handler = CreateHandler(shellBuilder, client, OutputFormat.Json);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("\"Name\"");
        output.ShouldContain("Admin");
    }

    private static ListRolesHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        OutputFormat outputFormat) =>
        new(
            shellBuilder.Build(),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);

    private static RoleResponse CreateRole(string name, string[] permissions) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Permissions = permissions,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
}