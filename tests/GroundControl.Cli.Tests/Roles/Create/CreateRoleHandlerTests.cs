using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Roles.Create;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Roles.Create;

public sealed class CreateRoleHandlerTests
{
    [Fact]
    public async Task HandleAsync_AllOptions_CreatesRole()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateRoleHandlerAsync(Arg.Any<CreateRoleRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RoleResponse
            {
                Id = Guid.CreateVersion7(),
                Name = "Editor",
                Permissions = ["scopes:read", "scopes:write"],
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new CreateRoleOptions { Name = "Editor", Permissions = "scopes:read,scopes:write" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("Editor");
        shellBuilder.GetOutput().ShouldContain("created");
        await client.Received(1).CreateRoleHandlerAsync(
            Arg.Is<CreateRoleRequest>(r =>
                r.Name == "Editor" &&
                r.Permissions.Count == 2 &&
                r.Permissions.Contains("scopes:read") &&
                r.Permissions.Contains("scopes:write")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithDescription_CreatesRoleWithDescription()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateRoleHandlerAsync(Arg.Any<CreateRoleRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RoleResponse
            {
                Id = Guid.CreateVersion7(),
                Name = "Viewer",
                Description = "Read-only access",
                Permissions = ["scopes:read"],
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new CreateRoleOptions { Name = "Viewer", Permissions = "scopes:read", Description = "Read-only access" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).CreateRoleHandlerAsync(
            Arg.Is<CreateRoleRequest>(r => r.Description == "Read-only access"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_MissingName_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateRoleOptions { Permissions = "scopes:read" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("--name");
        await client.DidNotReceive().CreateRoleHandlerAsync(
            Arg.Any<CreateRoleRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_NoPermissions_CreatesWithEmptyPermissions()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateRoleHandlerAsync(Arg.Any<CreateRoleRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RoleResponse
            {
                Id = Guid.CreateVersion7(),
                Name = "Empty",
                Permissions = [],
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new CreateRoleOptions { Name = "Empty" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).CreateRoleHandlerAsync(
            Arg.Is<CreateRoleRequest>(r => r.Permissions.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ApiValidationError_ShowsProblemDetails()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateRoleHandlerAsync(Arg.Any<CreateRoleRequest>(), Arg.Any<CancellationToken>())
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
            new CreateRoleOptions { Name = "" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Validation failed");
    }

    private static CreateRoleHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        CreateRoleOptions options,
        bool noInteractive) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}