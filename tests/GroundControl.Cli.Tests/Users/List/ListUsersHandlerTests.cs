using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Users.List;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Tests.Users.List;

public sealed class ListUsersHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersTableWithColumns()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListUsersHandlerAsync(
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfUserResponse
            {
                Data =
                [
                    CreateUser("alice", "alice@example.com"),
                    CreateUser("bob", "bob@example.com")
                ],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Id");
        output.ShouldContain("Username");
        output.ShouldContain("Email");
        output.ShouldContain("alice");
        output.ShouldContain("bob");
    }

    [Fact]
    public async Task HandleAsync_JsonOutput_RendersJsonArray()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListUsersHandlerAsync(
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfUserResponse
            {
                Data = [CreateUser("alice", "alice@example.com")],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, OutputFormat.Json);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("\"Username\"");
        output.ShouldContain("alice");
    }

    private static ListUsersHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        OutputFormat outputFormat) =>
        new(
            shellBuilder.Build(),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);

    private static UserResponse CreateUser(string username, string email) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Username = username,
            Email = email,
            IsActive = true,
            Grants = [],
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
}