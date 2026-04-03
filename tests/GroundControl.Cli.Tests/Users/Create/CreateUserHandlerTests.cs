using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Users.Create;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Users.Create;

public sealed class CreateUserHandlerTests
{
    [Fact]
    public async Task HandleAsync_AllOptions_CreatesUser()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateUserHandlerAsync(Arg.Any<CreateUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UserResponse
            {
                Id = Guid.CreateVersion7(),
                Username = "alice",
                Email = "alice@example.com",
                IsActive = true,
                Grants = [],
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new CreateUserOptions { Username = "alice", Email = "alice@example.com", Password = "secret123" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("alice");
        shellBuilder.GetOutput().ShouldContain("created");
        await client.Received(1).CreateUserHandlerAsync(
            Arg.Is<CreateUserRequest>(r =>
                r.Username == "alice" &&
                r.Email == "alice@example.com" &&
                r.Password == "secret123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithGrants_CreatesUserWithGrants()
    {
        // Arrange
        var roleId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateUserHandlerAsync(Arg.Any<CreateUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UserResponse
            {
                Id = Guid.CreateVersion7(),
                Username = "alice",
                Email = "alice@example.com",
                IsActive = true,
                Grants = [new GrantDto { RoleId = roleId }],
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new CreateUserOptions
            {
                Username = "alice",
                Email = "alice@example.com",
                Password = "secret123",
                Grants = [roleId.ToString()]
            },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).CreateUserHandlerAsync(
            Arg.Is<CreateUserRequest>(r => r.Grants != null && r.Grants.Count == 1 && r.Grants.First().RoleId == roleId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_MissingUsername_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateUserOptions { Email = "alice@example.com" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("--username");
        await client.DidNotReceive().CreateUserHandlerAsync(
            Arg.Any<CreateUserRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_MissingEmail_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateUserOptions { Username = "alice" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("--email");
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_MissingBoth_ListsAllMissing()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateUserOptions(),
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("--username");
        output.ShouldContain("--email");
    }

    // Interactive create prompt tests are not possible with MockShellBuilder because
    // Spectre.Console's PromptForStringAsync/PromptForSecretAsync requires real console input (ReadKey).

    [Fact]
    public async Task HandleAsync_InvalidGrantId_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateUserOptions
            {
                Username = "alice",
                Email = "alice@example.com",
                Password = "secret123",
                Grants = ["not-a-guid"]
            },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Invalid role ID");
        shellBuilder.GetOutput().ShouldContain("not-a-guid");
        await client.DidNotReceive().CreateUserHandlerAsync(
            Arg.Any<CreateUserRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ApiValidationError_ShowsProblemDetails()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreateUserHandlerAsync(Arg.Any<CreateUserRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<HttpValidationProblemDetails>(
                "Bad Request", 400, null, new Dictionary<string, IEnumerable<string>>(),
                new HttpValidationProblemDetails
                {
                    Status = 400,
                    Detail = "Validation failed.",
                    Errors = new Dictionary<string, ICollection<string>>
                    {
                        ["Username"] = ["Username is required."]
                    }
                }, null));

        var handler = CreateHandler(shellBuilder, client,
            new CreateUserOptions { Username = "", Email = "alice@example.com" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Validation failed");
    }

    private static CreateUserHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        CreateUserOptions options,
        bool noInteractive) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}